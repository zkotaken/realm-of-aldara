// Realm of Aldara Launcher for Windows.
// Two programs from one build. The launcher (news, updates and hotfixes, settings) installs the game into the
// user's AppData folder and keeps it updated; Play copies the game program next to the game files as
// "Realm of Aldara.exe", starts it and closes the launcher. The game runs in its own native window (drawn by the
// WebView2 engine that ships with Windows) with F11 fullscreen and no browser, served on a private local port;
// saves live in AppData. If WebView2 is missing, it falls back to an app-mode Edge or Chrome window.
package main

import (
	_ "embed"
	"encoding/json"
	"fmt"
	"io"
	"net"
	"net/http"
	"os"
	"os/exec"
	"path/filepath"
	"regexp"
	"runtime"
	"strings"
	"sync"
	"time"
)

const appName = "RealmOfAldara"

var (
	lastPing   = time.Now()
	pingMu     sync.Mutex
	gotPing    bool
	safeName   = regexp.MustCompile(`[^a-zA-Z0-9_\-]`)
	saveDir    string
	profileDir string
	rootDir    string
	restartCh  = make(chan struct{}, 1)
	quitCh     = make(chan struct{}, 1)
	listener   net.Listener
	gameMode   bool
)

func hasArg(a string) bool {
	for _, x := range os.Args[1:] {
		if x == a {
			return true
		}
	}
	return false
}

// the game program's file name inside the install folder
func gameExeName() string {
	if runtime.GOOS == "windows" {
		return "Realm of Aldara.exe"
	}
	return "realm-of-aldara-game"
}

// arguments a restarted copy of this program gets (a graphics card switch restarts straight back in)
func restartArgs() []string {
	if gameMode {
		return []string{"--game", "--restarted"}
	}
	return []string{"--restarted"}
}

type gpuInfo struct {
	Name string `json:"name"`
	Luid string `json:"luid"`
	VRAM uint64 `json:"vram"`
}

type launcherSettings struct {
	GPU    string `json:"gpu"` // default, high, low or luid:<high>,<low>
	Feed   string `json:"feed,omitempty"`
	NoAuto bool   `json:"noAuto,omitempty"`
}

func saveSettings(s launcherSettings) {
	b, _ := json.Marshal(s)
	os.WriteFile(settingsPath(), b, 0644)
}

func settingsPath() string { return filepath.Join(rootDir, "launcher.json") }

func loadSettings() launcherSettings {
	s := launcherSettings{GPU: "default"}
	if b, err := os.ReadFile(settingsPath()); err == nil {
		json.Unmarshal(b, &s)
	}
	if s.GPU == "" {
		s.GPU = "default"
	}
	return s
}

func handleGpus(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")
	json.NewEncoder(w).Encode(map[string]interface{}{"current": loadSettings().GPU, "adapters": listAdapters()})
}

var validGPU = regexp.MustCompile(`^(default|high|low|luid:-?[0-9]+,[0-9]+)$`)

func handleSetGpu(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		w.WriteHeader(405)
		return
	}
	var in launcherSettings
	if err := json.NewDecoder(io.LimitReader(r.Body, 4096)).Decode(&in); err != nil || !validGPU.MatchString(in.GPU) {
		http.Error(w, "bad setting", 400)
		return
	}
	s := loadSettings()
	s.GPU = in.GPU
	saveSettings(s)
	w.WriteHeader(204)
}

// the title menu's Quit button: close the window and stop
func handleQuit(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		w.WriteHeader(405)
		return
	}
	select {
	case quitCh <- struct{}{}:
	default:
	}
	w.WriteHeader(204)
}

func handleRestart(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		w.WriteHeader(405)
		return
	}
	select {
	case restartCh <- struct{}{}:
	default:
	}
	w.WriteHeader(204)
}

// browser switches for the chosen graphics card
func gpuArgs() []string {
	g := loadSettings().GPU
	switch {
	case g == "high":
		return []string{"--force_high_performance_gpu"}
	case g == "low":
		return []string{"--force_low_power_gpu"}
	case strings.HasPrefix(g, "luid:"):
		return []string{"--use-adapter-luid=" + strings.TrimPrefix(g, "luid:"), "--ignore-gpu-blocklist"}
	}
	return nil
}

// close the game window politely, then firmly if it lingers
func closeWindow(cmd *exec.Cmd, done chan error) {
	if runtime.GOOS == "windows" {
		exec.Command("taskkill", "/PID", fmt.Sprint(cmd.Process.Pid), "/T").Run()
	} else {
		cmd.Process.Signal(os.Interrupt)
	}
	select {
	case <-done:
	case <-time.After(4 * time.Second):
		if runtime.GOOS == "windows" {
			exec.Command("taskkill", "/F", "/PID", fmt.Sprint(cmd.Process.Pid), "/T").Run()
		} else {
			cmd.Process.Kill()
		}
		<-done
	}
	time.Sleep(1500 * time.Millisecond) // let the browser release its profile
}

func dataDir() string {
	base := os.Getenv("APPDATA")
	if base == "" {
		if h, err := os.UserConfigDir(); err == nil {
			base = h
		} else {
			base = "."
		}
	}
	return filepath.Join(base, appName)
}

func savePath(p string) string {
	return filepath.Join(saveDir, safeName.ReplaceAllString(p, "_")+".json")
}

func handleSave(w http.ResponseWriter, r *http.Request) {
	p := r.URL.Query().Get("path")
	if p == "" {
		http.Error(w, "missing path", 400)
		return
	}
	f := savePath(p)
	switch r.Method {
	case http.MethodGet:
		b, err := os.ReadFile(f)
		if err != nil {
			w.WriteHeader(404)
			return
		}
		w.Header().Set("Content-Type", "application/json")
		w.Write(b)
	case http.MethodPut, http.MethodPost:
		b, err := io.ReadAll(io.LimitReader(r.Body, 8<<20))
		if err != nil || !json.Valid(b) {
			http.Error(w, "bad save", 400)
			return
		}
		tmp := f + ".tmp"
		if err := os.WriteFile(tmp, b, 0644); err != nil {
			http.Error(w, err.Error(), 500)
			return
		}
		os.Rename(tmp, f)
		w.WriteHeader(204)
	case http.MethodDelete:
		os.Remove(f)
		w.WriteHeader(204)
	default:
		w.WriteHeader(405)
	}
}

func handlePing(w http.ResponseWriter, r *http.Request) {
	pingMu.Lock()
	lastPing = time.Now()
	gotPing = true
	pingMu.Unlock()
	w.Header().Set("Content-Type", "text/plain")
	w.Write([]byte("aldara"))
}

func handleGame(w http.ResponseWriter, r *http.Request) {
	if r.URL.Path != "/" {
		http.NotFound(w, r)
		return
	}
	w.Header().Set("Content-Type", "text/html; charset=utf-8")
	w.Header().Set("Cache-Control", "no-store")
	g := currentGame()
	if g == "" {
		w.Write([]byte(notInstalledPage))
		return
	}
	inject := "<script>window.ALDARA_NATIVE=true;</script>"
	html := strings.Replace(g, "<head>", "<head>"+inject, 1)
	w.Write([]byte(html))
}

// find a browser that can open a chromeless app window
func appBrowser() string {
	if runtime.GOOS != "windows" {
		return ""
	}
	var cands []string
	for _, env := range []string{"ProgramFiles(x86)", "ProgramFiles", "LOCALAPPDATA"} {
		b := os.Getenv(env)
		if b == "" {
			continue
		}
		cands = append(cands,
			filepath.Join(b, "Microsoft", "Edge", "Application", "msedge.exe"),
			filepath.Join(b, "Google", "Chrome", "Application", "chrome.exe"))
	}
	for _, c := range cands {
		if _, err := os.Stat(c); err == nil {
			return c
		}
	}
	return ""
}

func openDefault(url string) {
	switch runtime.GOOS {
	case "windows":
		exec.Command("rundll32", "url.dll,FileProtocolHandler", url).Start()
	case "darwin":
		exec.Command("open", url).Start()
	default:
		exec.Command("xdg-open", url).Start()
	}
}

func main() {
	root := dataDir()
	rootDir = root
	saveDir = filepath.Join(root, "saves")
	gameMode = hasArg("--game") || strings.EqualFold(filepath.Base(os.Args[0]), gameExeName())
	// the game keeps the port and window profile it always had, so its settings carry over
	profileDir = filepath.Join(root, "window")
	port := "47821"
	if !gameMode {
		profileDir = filepath.Join(root, "launcher-window")
		port = "47820"
	}
	os.MkdirAll(saveDir, 0755)
	loadInstalledGame()
	loadClassic()
	if self, err := os.Executable(); err == nil && !gameMode {
		os.Remove(self + ".old")
		os.Remove(self + ".new")
	}

	// a fixed port keeps things tidy; fall back to any free port if it is taken
	ln, err := net.Listen("tcp", "127.0.0.1:"+port)
	if err != nil {
		ln, err = net.Listen("tcp", "127.0.0.1:0")
		if err != nil {
			fmt.Println("Could not start the game server:", err)
			os.Exit(1)
		}
	}
	listener = ln
	url := "http://" + ln.Addr().String() + "/launcher"
	title := "Realm of Aldara Launcher"
	if gameMode {
		url = "http://" + ln.Addr().String() + "/"
		title = "Realm of Aldara"
	}

	mux := http.NewServeMux()
	mux.HandleFunc("/", handleGame)
	mux.HandleFunc("/api/save", handleSave)
	mux.HandleFunc("/api/ping", handlePing)
	mux.HandleFunc("/api/gpus", handleGpus)
	mux.HandleFunc("/api/gpu", handleSetGpu)
	mux.HandleFunc("/api/restart", handleRestart)
	mux.HandleFunc("/api/quit", handleQuit)
	mux.HandleFunc("/launcher", handleLauncher)
	mux.HandleFunc("/api/launcher/state", handleLState)
	mux.HandleFunc("/api/launcher/check", handleLCheck)
	mux.HandleFunc("/api/launcher/update", handleLUpdate)
	mux.HandleFunc("/api/launcher/notes", handleLNotes)
	mux.HandleFunc("/api/launcher/settings", handleLSettings)
	mux.HandleFunc("/api/launcher/saves", handleOpenSaves)
	mux.HandleFunc("/api/launcher/play", handleLPlay)
	mux.HandleFunc("/api/launcher/classic/update", handleClassicUpdate)
	mux.HandleFunc("/api/launcher/classic/play", handleClassicPlay)
	mux.Handle("/music/", http.StripPrefix("/music/", http.FileServer(http.Dir(filepath.Join(gameDir(), "music")))))
	go http.Serve(ln, mux)

	if os.Getenv("ALDARA_NOWINDOW") != "" { // used for testing
		fmt.Println(url)
		select {}
	}

	if runWindow(url, title) {
		os.Exit(0)
	}

	if b := appBrowser(); b != "" {
		for {
			args := append([]string{"--app=" + url, "--user-data-dir=" + profileDir, "--window-size=1400,880",
				"--no-first-run", "--no-default-browser-check", "--disable-features=Translate",
				"--hide-crash-restore-bubble", "--disable-session-crashed-bubble"}, gpuArgs()...)
			cmd := exec.Command(b, args...)
			if err := cmd.Start(); err != nil {
				break
			}
			done := make(chan error, 1)
			go func() { done <- cmd.Wait() }()
			restart := false
			select {
			case <-done:
				// the window closed (or handed off to an already running browser)
			case <-restartCh:
				closeWindow(cmd, done)
				restart = true
			case <-quitCh:
				time.Sleep(300 * time.Millisecond)
				closeWindow(cmd, done)
				os.Exit(0)
			}
			if !restart {
				break
			}
			pingMu.Lock()
			lastPing = time.Now()
			pingMu.Unlock()
		}
	} else {
		openDefault(url)
	}
	// exit once the game has stopped checking in (window closed)
	for {
		time.Sleep(3 * time.Second)
		pingMu.Lock()
		idle := time.Since(lastPing)
		seen := gotPing
		pingMu.Unlock()
		if (seen && idle > 20*time.Second) || (!seen && idle > 120*time.Second) {
			os.Exit(0)
		}
	}
}

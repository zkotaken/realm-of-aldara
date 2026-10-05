// The launcher and updater: the launcher page (news, updates and hotfixes, settings) installs the game from an
// online feed into AppData, verifies it, and keeps it updated, so players never have to re-download the program.
// Play starts the separate game program and closes the launcher.
package main

import (
	"crypto/sha256"
	_ "embed"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strconv"
	"strings"
	"sync"
	"time"
)

//go:embed launcher.html
var launcherHTML string

//go:embed patchnotes.json
var embeddedNotes []byte

//go:embed version.txt
var embeddedVersion string

const defaultFeed = "https://raw.githubusercontent.com/zkotaken/realm-of-aldara/main/"

type feedVersion struct {
	Version string `json:"version"`
	Size    int64  `json:"size"`
	SHA256  string `json:"sha256"`
	File    string `json:"file"`
	Date    string `json:"date"`
	Title   string `json:"title"`
	// the launcher page itself can be updated too
	Launcher       string      `json:"launcher"`
	LauncherSHA256 string      `json:"launcher_sha256"`
	Exe            string      `json:"exe"`
	ExeBuild       string      `json:"exe_build"`
	ExeSHA256      string      `json:"exe_sha256"`
	Assets         []feedAsset `json:"assets"`
	// the Unity edition, installed beside the browser edition (classic.go)
	Classic *feedClassic `json:"classic,omitempty"`
}

const launcherPageVersion = 6

// the program itself: the launcher replaces itself (and so the game program it copies on Play) with newer builds
const exeBuild = 5

func launcherVer(v string) int { n, _ := strconv.Atoi(strings.TrimSpace(v)); return n }

// the newest launcher page: a downloaded one if it is newer than the one built in
func currentLauncher() string {
	b, err := os.ReadFile(filepath.Join(gameDir(), "launcher.html"))
	if err != nil {
		return launcherHTML
	}
	vb, _ := os.ReadFile(filepath.Join(gameDir(), "launcher.version"))
	if launcherVer(string(vb)) <= launcherPageVersion {
		return launcherHTML
	}
	return string(b)
}

func fetchLauncher(v feedVersion) {
	if launcherVer(v.Launcher) <= launcherPageVersion {
		return
	}
	if vb, _ := os.ReadFile(filepath.Join(gameDir(), "launcher.version")); launcherVer(string(vb)) >= launcherVer(v.Launcher) {
		return
	}
	r, err := httpc.Get(feedURL() + "launcher.html?v=" + v.Launcher)
	if err != nil {
		return
	}
	defer r.Body.Close()
	if r.StatusCode != 200 {
		return
	}
	b, err := io.ReadAll(io.LimitReader(r.Body, 20<<20))
	if err != nil || len(b) < 1000 {
		return
	}
	sum := sha256.Sum256(b)
	if v.LauncherSHA256 != "" && !strings.EqualFold(hex.EncodeToString(sum[:]), v.LauncherSHA256) {
		return
	}
	os.MkdirAll(gameDir(), 0755)
	os.WriteFile(filepath.Join(gameDir(), "launcher.html"), b, 0644)
	os.WriteFile(filepath.Join(gameDir(), "launcher.version"), []byte(v.Launcher), 0644)
}

var upd struct {
	sync.Mutex
	busy     bool
	got, tot int64
	err      string
	latest   *feedVersion
	checked  bool
	checkErr string
	exeBusy  bool
	game     string // the game html currently served
	ver      string // its version
}

func gameDir() string { return filepath.Join(rootDir, "game") }

// version strings like "142" or "142.1" (hotfix); compare numerically part by part
func verLess(a, b string) bool {
	pa, pb := strings.Split(strings.TrimSpace(a), "."), strings.Split(strings.TrimSpace(b), ".")
	for i := 0; i < len(pa) || i < len(pb); i++ {
		var x, y int
		if i < len(pa) {
			x, _ = strconv.Atoi(pa[i])
		}
		if i < len(pb) {
			y, _ = strconv.Atoi(pb[i])
		}
		if x != y {
			return x < y
		}
	}
	return false
}

// the installed game (nothing until the launcher has installed it)
func loadInstalledGame() {
	game, ver := "", ""
	if b, err := os.ReadFile(filepath.Join(gameDir(), "version.json")); err == nil {
		var v feedVersion
		if json.Unmarshal(b, &v) == nil && v.Version != "" {
			if h, err := os.ReadFile(filepath.Join(gameDir(), "game.html")); err == nil && len(h) > 1000 {
				sum := sha256.Sum256(h)
				if v.SHA256 == "" || strings.EqualFold(hex.EncodeToString(sum[:]), v.SHA256) {
					game, ver = string(h), v.Version
				}
			}
		}
	}
	upd.Lock()
	upd.game, upd.ver = game, ver
	upd.Unlock()
}

func currentGame() string {
	upd.Lock()
	defer upd.Unlock()
	return upd.game
}

func feedURL() string {
	f := strings.TrimSpace(loadSettings().Feed)
	if f == "" {
		f = defaultFeed
	}
	if !strings.HasSuffix(f, "/") {
		f += "/"
	}
	return f
}

var httpc = &http.Client{Timeout: 12 * time.Second}

func fetchJSON(url string, v interface{}) error {
	req, _ := http.NewRequest("GET", url+"?t="+strconv.FormatInt(time.Now().Unix(), 10), nil)
	req.Header.Set("Cache-Control", "no-cache")
	r, err := httpc.Do(req)
	if err != nil {
		return err
	}
	defer r.Body.Close()
	if r.StatusCode != 200 {
		return fmt.Errorf("the update server answered %d", r.StatusCode)
	}
	return json.NewDecoder(io.LimitReader(r.Body, 4<<20)).Decode(v)
}

var exeUpdated bool

// download a newer launcher program, verify it, and swap it in for this one (a running program can be renamed on
// Windows); the next launch, and the game started by Play, use the new build
func fetchExe(v feedVersion) {
	if v.Exe == "" || launcherVer(v.ExeBuild) <= exeBuild || v.ExeSHA256 == "" || (runtime.GOOS != "windows" && os.Getenv("ALDARA_EXETEST") == "") {
		return
	}
	upd.Lock()
	if upd.exeBusy || exeUpdated {
		upd.Unlock()
		return
	}
	upd.exeBusy = true
	upd.Unlock()
	defer func() { upd.Lock(); upd.exeBusy = false; upd.Unlock() }()
	self, err := os.Executable()
	if err != nil {
		return
	}
	if fileSum(self) == strings.ToLower(v.ExeSHA256) {
		return
	}
	dl := &http.Client{Timeout: 10 * time.Minute}
	r, err := dl.Get(feedURL() + strings.ReplaceAll(v.Exe, " ", "%20") + "?b=" + v.ExeBuild)
	if err != nil {
		return
	}
	defer r.Body.Close()
	if r.StatusCode != 200 {
		return
	}
	tmp := self + ".new"
	f, err := os.OpenFile(tmp, os.O_CREATE|os.O_TRUNC|os.O_WRONLY, 0755)
	if err != nil {
		return
	}
	h := sha256.New()
	_, err = io.Copy(io.MultiWriter(f, h), io.LimitReader(r.Body, 200<<20))
	f.Close()
	if err != nil || !strings.EqualFold(hex.EncodeToString(h.Sum(nil)), v.ExeSHA256) {
		os.Remove(tmp)
		return
	}
	old := self + ".old"
	os.Remove(old)
	if os.Rename(self, old) != nil {
		os.Remove(tmp)
		return
	}
	if os.Rename(tmp, self) != nil {
		os.Rename(old, self)
		return
	}
	upd.Lock()
	exeUpdated = true
	upd.Unlock()
}

func handleLauncher(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "text/html; charset=utf-8")
	w.Header().Set("Cache-Control", "no-store")
	w.Write([]byte(currentLauncher()))
}

func handleLState(w http.ResponseWriter, r *http.Request) {
	s := loadSettings()
	cs := classicState()
	upd.Lock()
	out := map[string]interface{}{"classic": cs, "installed": upd.ver, "embedded": strings.TrimSpace(embeddedVersion), "feed": feedURL(), "auto": !s.NoAuto, "separate": true, "dir": gameDir(), "exeUpdated": exeUpdated, "build": exeBuild, "missing": len(missingAssets(upd.latest)),
		"busy": upd.busy, "got": upd.got, "tot": upd.tot, "err": upd.err, "checked": upd.checked, "checkErr": upd.checkErr, "latest": upd.latest}
	upd.Unlock()
	w.Header().Set("Content-Type", "application/json")
	json.NewEncoder(w).Encode(out)
}

// ask the feed what the newest build is, and refresh the patch notes
func handleLCheck(w http.ResponseWriter, r *http.Request) {
	var v feedVersion
	err := fetchJSON(feedURL()+"version.json", &v)
	upd.Lock()
	upd.checked = true
	if err != nil {
		upd.checkErr = err.Error()
	} else if v.Version == "" {
		upd.checkErr = "the update server sent no version"
	} else {
		upd.checkErr = ""
		upd.latest = &v
	}
	upd.Unlock()
	if err == nil {
		fetchLauncher(v)
		go fetchExe(v)
	}
	var notes json.RawMessage
	if fetchJSON(feedURL()+"patchnotes.json", &notes) == nil && json.Valid(notes) {
		os.MkdirAll(gameDir(), 0755)
		os.WriteFile(filepath.Join(gameDir(), "patchnotes.json"), notes, 0644)
	}
	handleLState(w, r)
}

func handleLNotes(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")
	w.Header().Set("Cache-Control", "no-store")
	if b, err := os.ReadFile(filepath.Join(gameDir(), "patchnotes.json")); err == nil && json.Valid(b) {
		// use whichever list is newer (a fresh exe may carry notes newer than an old download)
		var a, e struct {
			Entries []struct {
				V string `json:"v"`
			} `json:"entries"`
		}
		json.Unmarshal(b, &a)
		json.Unmarshal(embeddedNotes, &e)
		if len(a.Entries) > 0 && (len(e.Entries) == 0 || !verLess(a.Entries[0].V, e.Entries[0].V)) {
			w.Write(b)
			return
		}
	}
	w.Write(embeddedNotes)
}

type countWriter struct{ n *int64 }

func (c countWriter) Write(p []byte) (int, error) {
	upd.Lock()
	*c.n += int64(len(p))
	upd.Unlock()
	return len(p), nil
}

// extra game files (music) listed by the feed; kept in the game folder and checked by their checksums
type feedAsset struct {
	Path   string `json:"p"`
	SHA256 string `json:"s"`
	Size   int64  `json:"n"`
}

var assetSum = map[string]string{} // path -> checksum of the file on disk, so checks stay quick

func assetLocal(a feedAsset) string {
	clean := filepath.Clean(filepath.FromSlash(a.Path))
	if strings.HasPrefix(clean, "..") || filepath.IsAbs(clean) {
		return ""
	}
	return filepath.Join(gameDir(), clean)
}

func missingAssets(v *feedVersion) []feedAsset {
	var out []feedAsset
	if v == nil {
		return nil
	}
	for _, a := range v.Assets {
		p := assetLocal(a)
		if p == "" {
			continue
		}
		st, err := os.Stat(p)
		if err != nil || st.Size() != a.Size {
			out = append(out, a)
			continue
		}
		key := p + "|" + strconv.FormatInt(st.ModTime().UnixNano(), 10)
		sum, ok := assetSum[key]
		if !ok {
			sum = fileSum(p)
			assetSum[key] = sum
		}
		if !strings.EqualFold(sum, a.SHA256) {
			out = append(out, a)
		}
	}
	return out
}

func download(url, dst, sum string) error {
	dl := &http.Client{Timeout: 15 * time.Minute}
	resp, err := dl.Get(url)
	if err != nil {
		return fmt.Errorf("Download failed: %v", err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != 200 {
		return fmt.Errorf("Download failed: the server answered %d", resp.StatusCode)
	}
	os.MkdirAll(filepath.Dir(dst), 0755)
	tmp := dst + ".download"
	f, err := os.Create(tmp)
	if err != nil {
		return fmt.Errorf("Could not write the update: %v", err)
	}
	h := sha256.New()
	_, err = io.Copy(io.MultiWriter(f, h, countWriter{&upd.got}), io.LimitReader(resp.Body, 200<<20))
	f.Close()
	if err != nil {
		os.Remove(tmp)
		return fmt.Errorf("Download interrupted: %v", err)
	}
	if sum != "" && !strings.EqualFold(hex.EncodeToString(h.Sum(nil)), sum) {
		os.Remove(tmp)
		return fmt.Errorf("The download was damaged (checksum mismatch). Try again.")
	}
	os.Remove(dst)
	if err := os.Rename(tmp, dst); err != nil {
		return fmt.Errorf("Could not install the update: %v", err)
	}
	return nil
}

// download the newest build and any missing music in the background; the launcher polls the state for progress
func handleLUpdate(w http.ResponseWriter, r *http.Request) {
	upd.Lock()
	v := upd.latest
	if upd.busy || v == nil {
		upd.Unlock()
		handleLState(w, r)
		return
	}
	needGame := verLess(upd.ver, v.Version)
	miss := missingAssets(v)
	if !needGame && len(miss) == 0 {
		upd.Unlock()
		handleLState(w, r)
		return
	}
	tot := int64(0)
	if needGame {
		tot = v.Size
	}
	for _, a := range miss {
		tot += a.Size
	}
	upd.busy, upd.got, upd.tot, upd.err = true, 0, tot, ""
	upd.Unlock()
	go func(v feedVersion) {
		fail := func(e string) { upd.Lock(); upd.busy = false; upd.err = e; upd.Unlock() }
		if needGame {
			file := v.File
			if file == "" {
				file = "game.html"
			}
			if err := download(feedURL()+file+"?v="+v.Version, filepath.Join(gameDir(), "game.html"), v.SHA256); err != nil {
				fail(err.Error())
				return
			}
			b, _ := json.Marshal(v)
			os.WriteFile(filepath.Join(gameDir(), "version.json"), b, 0644)
			loadInstalledGame()
		}
		for _, a := range miss {
			p := assetLocal(a)
			if p == "" {
				continue
			}
			if err := download(feedURL()+strings.ReplaceAll(a.Path, " ", "%20")+"?v="+v.Version, p, a.SHA256); err != nil {
				fail(err.Error())
				return
			}
		}
		upd.Lock()
		upd.busy = false
		upd.Unlock()
	}(*v)
	handleLState(w, r)
}

func handleLSettings(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		w.WriteHeader(405)
		return
	}
	var in struct {
		Feed *string `json:"feed"`
		Auto *bool   `json:"auto"`
	}
	if json.NewDecoder(io.LimitReader(r.Body, 4096)).Decode(&in) != nil {
		http.Error(w, "bad settings", 400)
		return
	}
	s := loadSettings()
	if in.Feed != nil {
		f := strings.TrimSpace(*in.Feed)
		if f != "" && !strings.HasPrefix(f, "https://") && !strings.HasPrefix(f, "http://127.0.0.1") {
			http.Error(w, "the update source must start with https://", 400)
			return
		}
		s.Feed = f
	}
	if in.Auto != nil {
		s.NoAuto = !*in.Auto
	}
	saveSettings(s)
	handleLState(w, r)
}

// Play: put the game program next to the game files (refreshing it when the launcher is newer), start it, and close
// the launcher. If the game is already open, just close the launcher.
func handleLPlay(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		w.WriteHeader(405)
		return
	}
	upd.Lock()
	ok := upd.game != "" && !upd.busy
	upd.Unlock()
	if !ok {
		http.Error(w, "The game is not installed yet.", 409)
		return
	}
	quit := func() {
		go func() {
			time.Sleep(600 * time.Millisecond)
			select {
			case quitCh <- struct{}{}:
			default:
			}
		}()
	}
	if c, err := (&http.Client{Timeout: 700 * time.Millisecond}).Get("http://127.0.0.1:47821/api/ping"); err == nil {
		b, _ := io.ReadAll(io.LimitReader(c.Body, 64))
		c.Body.Close()
		if string(b) == "aldara" {
			quit()
			w.WriteHeader(204)
			return
		}
	}
	exe, err := installGameExe()
	if err != nil {
		http.Error(w, "Could not set up the game program: "+err.Error(), 500)
		return
	}
	cmd := exec.Command(exe, "--game")
	cmd.Dir = gameDir()
	if err := cmd.Start(); err != nil {
		http.Error(w, "Could not start the game: "+err.Error(), 500)
		return
	}
	quit()
	w.WriteHeader(204)
}

func fileSum(p string) string {
	f, err := os.Open(p)
	if err != nil {
		return ""
	}
	defer f.Close()
	h := sha256.New()
	io.Copy(h, f)
	return hex.EncodeToString(h.Sum(nil))
}

func installGameExe() (string, error) {
	self, err := os.Executable()
	if err != nil {
		return "", err
	}
	os.MkdirAll(gameDir(), 0755)
	dst := filepath.Join(gameDir(), gameExeName())
	if fileSum(dst) == fileSum(self) && fileSum(dst) != "" {
		return dst, nil
	}
	in, err := os.Open(self)
	if err != nil {
		return "", err
	}
	defer in.Close()
	tmp := dst + ".new"
	out, err := os.OpenFile(tmp, os.O_CREATE|os.O_TRUNC|os.O_WRONLY, 0755)
	if err != nil {
		return "", err
	}
	_, err = io.Copy(out, in)
	out.Close()
	if err != nil {
		os.Remove(tmp)
		return "", err
	}
	os.Remove(dst)
	if err := os.Rename(tmp, dst); err != nil {
		os.Remove(tmp)
		if _, e2 := os.Stat(dst); e2 == nil {
			return dst, nil // an older copy is still in use; it works the same
		}
		return "", err
	}
	return dst, nil
}

const notInstalledPage = `<!doctype html><html><head><meta charset="utf-8"><title>Realm of Aldara</title></head>
<body style="margin:0;background:#0b0d14;color:#e8dcc0;font:16px system-ui,sans-serif;display:flex;align-items:center;justify-content:center;height:100vh;text-align:center">
<div><h2 style="font-weight:600">Realm of Aldara is not installed</h2><p>Open the Realm of Aldara Launcher and press Install.</p></div></body></html>`

func handleOpenSaves(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		w.WriteHeader(405)
		return
	}
	if runtime.GOOS == "windows" {
		exec.Command("explorer", saveDir).Start()
	} else {
		openDefault(saveDir)
	}
	w.WriteHeader(204)
}

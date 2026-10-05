// Realm of Aldara Classic: the Unity edition of the game, installed and kept updated by the launcher next to the
// browser edition. The feed (version.json) names it under "classic" with a manifest: every file of the Windows
// game with its checksum, stored online as gzip parts named by their own checksum. Install and update fetch only
// the files that changed, verify every part and every file, build the new copy beside the old one (unchanged files
// are linked or copied across) and swap it in, so an interrupted update never breaks the copy that works.
// Play Classic starts it and closes the launcher. Classic reads the same saves folder as the browser edition, so
// characters carry over.
package main

import (
	"bytes"
	"compress/gzip"
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"sync"
	"time"
)

type feedClassic struct {
	Version     string `json:"version"`
	Title       string `json:"title"`
	Exe         string `json:"exe"`
	Base        string `json:"base"`         // where the parts live (ends with /)
	Manifest    string `json:"manifest"`     // path of the manifest under Base
	ManifestSum string `json:"manifest_sha"` // its checksum
	Size        int64  `json:"size"`         // the whole download, for display
}

type clPart struct {
	H string `json:"h"` // checksum of the gzip part (and its name, parts/<h>.gz)
	N int64  `json:"n"` // gzip size
	R int64  `json:"r"` // size once unpacked
}
type clFile struct {
	P     string   `json:"p"`
	S     string   `json:"s"`
	N     int64    `json:"n"`
	Parts []clPart `json:"parts"`
}
type clManifest struct {
	Version string   `json:"version"`
	Exe     string   `json:"exe"`
	Files   []clFile `json:"files"`
}

var cl struct {
	sync.Mutex
	busy     bool
	got, tot int64
	phase    string // "check", "download" or "install"
	err      string
	ver      string
}

func classicDir() string { return filepath.Join(rootDir, "classic") }

func exeOr(n string) string {
	if n == "" {
		return "Realm of Aldara.exe"
	}
	return filepath.Base(n)
}

// the installed Classic version (written only after a complete, verified install)
func loadClassic() {
	b, _ := os.ReadFile(filepath.Join(classicDir(), "classic.version"))
	cl.Lock()
	cl.ver = strings.TrimSpace(string(b))
	cl.Unlock()
}

func classicState() map[string]interface{} {
	cl.Lock()
	out := map[string]interface{}{"installed": cl.ver, "busy": cl.busy, "got": cl.got, "tot": cl.tot, "phase": cl.phase, "err": cl.err, "dir": classicDir()}
	cl.Unlock()
	upd.Lock()
	if upd.latest != nil && upd.latest.Classic != nil {
		out["latest"] = upd.latest.Classic
	}
	upd.Unlock()
	return out
}

type clCount struct{}

func (clCount) Write(p []byte) (int, error) {
	cl.Lock()
	cl.got += int64(len(p))
	cl.Unlock()
	return len(p), nil
}

func handleClassicUpdate(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		w.WriteHeader(405)
		return
	}
	upd.Lock()
	var v *feedClassic
	if upd.latest != nil {
		v = upd.latest.Classic
	}
	upd.Unlock()
	cl.Lock()
	if v == nil || v.Manifest == "" || cl.busy || (cl.ver != "" && !verLess(cl.ver, v.Version)) {
		cl.Unlock()
		handleLState(w, r)
		return
	}
	cl.busy, cl.got, cl.tot, cl.err, cl.phase = true, 0, 0, "", "check"
	cl.Unlock()
	go func(v feedClassic) {
		err := installClassic(v)
		cl.Lock()
		cl.busy = false
		if err != nil {
			cl.err = err.Error()
		} else {
			cl.err = ""
			cl.ver = v.Version
		}
		cl.Unlock()
	}(*v)
	handleLState(w, r)
}

func baseURL(v feedClassic) string {
	b := strings.TrimSpace(v.Base)
	if b == "" {
		b = feedURL()
	}
	if !strings.HasSuffix(b, "/") {
		b += "/"
	}
	return b
}

func fetchManifest(v feedClassic) (*clManifest, error) {
	c := &http.Client{Timeout: 60 * time.Second}
	resp, err := c.Get(baseURL(v) + v.Manifest)
	if err != nil {
		return nil, fmt.Errorf("Could not reach the update server: %v", err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != 200 {
		return nil, fmt.Errorf("The update server answered %d", resp.StatusCode)
	}
	b, err := io.ReadAll(io.LimitReader(resp.Body, 32<<20))
	if err != nil {
		return nil, fmt.Errorf("Download interrupted: %v", err)
	}
	if v.ManifestSum != "" {
		s := sha256.Sum256(b)
		if !strings.EqualFold(hex.EncodeToString(s[:]), v.ManifestSum) {
			return nil, fmt.Errorf("The update list was damaged. Try again.")
		}
	}
	var m clManifest
	if err := json.Unmarshal(b, &m); err != nil || len(m.Files) == 0 {
		return nil, fmt.Errorf("The update list could not be read.")
	}
	return &m, nil
}

// a path from the manifest, kept inside dir
func inside(dir, p string) (string, bool) {
	clean := filepath.Clean(filepath.FromSlash(p))
	if clean == "." || strings.HasPrefix(clean, "..") || filepath.IsAbs(clean) {
		return "", false
	}
	return filepath.Join(dir, clean), true
}

func installClassic(v feedClassic) error {
	m, err := fetchManifest(v)
	if err != nil {
		return err
	}
	cur, next := classicDir(), classicDir()+".new"
	os.RemoveAll(next)
	if err := os.MkdirAll(next, 0755); err != nil {
		return fmt.Errorf("Could not write the game files: %v", err)
	}
	// which files the installed copy already has (same size and checksum), at the same path or any other (a renamed
	// file or folder is reused instead of downloaded again)
	bySize := map[int64][]string{}
	filepath.Walk(cur, func(p string, info os.FileInfo, err error) error {
		if err == nil && !info.IsDir() {
			bySize[info.Size()] = append(bySize[info.Size()], p)
		}
		return nil
	})
	sums := map[string]string{}
	sumOf := func(p string) string {
		if s, ok := sums[p]; ok {
			return s
		}
		s := fileSum(p)
		sums[p] = s
		return s
	}
	type haveFile struct {
		f   clFile
		src string
	}
	var need []clFile
	var have []haveFile
	for _, f := range m.Files {
		src, ok := inside(cur, f.P)
		if !ok {
			return fmt.Errorf("The update list has a bad path: %s", f.P)
		}
		found := ""
		if st, err := os.Stat(src); err == nil && st.Size() == f.N && strings.EqualFold(sumOf(src), f.S) {
			found = src
		} else {
			for _, c := range bySize[f.N] {
				if strings.EqualFold(sumOf(c), f.S) {
					found = c
					break
				}
			}
		}
		if found != "" {
			have = append(have, haveFile{f, found})
		} else {
			need = append(need, f)
		}
	}
	var tot int64
	for _, f := range need {
		for _, p := range f.Parts {
			tot += p.N
		}
	}
	cl.Lock()
	cl.phase, cl.tot, cl.got = "download", tot, 0
	cl.Unlock()
	dl := &http.Client{Timeout: 30 * time.Minute}
	for _, f := range need {
		dst, _ := inside(next, f.P)
		if err := fetchFile(dl, baseURL(v), f, dst); err != nil {
			os.RemoveAll(next)
			return err
		}
	}
	cl.Lock()
	cl.phase = "install"
	cl.Unlock()
	for _, h := range have {
		src := h.src
		dst, _ := inside(next, h.f.P)
		os.MkdirAll(filepath.Dir(dst), 0755)
		if os.Link(src, dst) != nil {
			if err := copyFile(src, dst); err != nil {
				os.RemoveAll(next)
				return fmt.Errorf("Could not copy the game files: %v", err)
			}
		}
	}
	if _, err := os.Stat(filepath.Join(next, exeOr(m.Exe))); err != nil {
		os.RemoveAll(next)
		return fmt.Errorf("The download did not contain the game program.")
	}
	os.WriteFile(filepath.Join(next, "classic.version"), []byte(v.Version), 0644)
	old := cur + ".old"
	os.RemoveAll(old)
	if _, err := os.Stat(cur); err == nil {
		if err := os.Rename(cur, old); err != nil {
			os.RemoveAll(next)
			return fmt.Errorf("Close Realm of Aldara Classic and try again.")
		}
	}
	if err := os.Rename(next, cur); err != nil {
		os.Rename(old, cur)
		return fmt.Errorf("Could not install the game: %v", err)
	}
	os.RemoveAll(old)
	return nil
}

// one file: its gzip parts in order, each verified, unpacked into place, then the whole file verified
func fetchFile(dl *http.Client, base string, f clFile, dst string) error {
	os.MkdirAll(filepath.Dir(dst), 0755)
	tmp := dst + ".download"
	out, err := os.Create(tmp)
	if err != nil {
		return fmt.Errorf("Could not write the game files: %v", err)
	}
	whole := sha256.New()
	for _, p := range f.Parts {
		if err := fetchPart(dl, base, p, io.MultiWriter(out, whole)); err != nil {
			out.Close()
			os.Remove(tmp)
			return err
		}
	}
	out.Close()
	if !strings.EqualFold(hex.EncodeToString(whole.Sum(nil)), f.S) {
		os.Remove(tmp)
		return fmt.Errorf("A game file was damaged in the download (%s). Try again.", filepath.Base(f.P))
	}
	os.Chmod(tmp, 0755)
	return os.Rename(tmp, dst)
}

func fetchPart(dl *http.Client, base string, p clPart, w io.Writer) error {
	var last error
	for try := 0; try < 3; try++ {
		if try > 0 {
			time.Sleep(time.Duration(try*2) * time.Second)
		}
		last = func() error {
			resp, err := dl.Get(base + "parts/" + p.H + ".gz")
			if err != nil {
				return fmt.Errorf("Download failed: %v", err)
			}
			defer resp.Body.Close()
			if resp.StatusCode != 200 {
				return fmt.Errorf("Download failed: the server answered %d", resp.StatusCode)
			}
			// the gzip part is checked as it arrives, then unpacked from memory
			h := sha256.New()
			buf, err := io.ReadAll(io.TeeReader(io.LimitReader(resp.Body, p.N+1), io.MultiWriter(h, clCount{})))
			if err != nil {
				return fmt.Errorf("Download interrupted: %v", err)
			}
			if int64(len(buf)) != p.N || !strings.EqualFold(hex.EncodeToString(h.Sum(nil)), p.H) {
				cl.Lock()
				cl.got -= int64(len(buf))
				cl.Unlock()
				return fmt.Errorf("The download was damaged (checksum mismatch). Try again.")
			}
			zr, err := gzip.NewReader(bytes.NewReader(buf))
			if err != nil {
				return fmt.Errorf("A download could not be unpacked: %v", err)
			}
			n, err := io.Copy(w, zr)
			if err != nil || n != p.R {
				return fmt.Errorf("A download could not be unpacked.")
			}
			return nil
		}()
		if last == nil {
			return nil
		}
		if strings.Contains(last.Error(), "unpacked") {
			return last
		}
	}
	return last
}

func copyFile(src, dst string) error {
	in, err := os.Open(src)
	if err != nil {
		return err
	}
	defer in.Close()
	out, err := os.Create(dst)
	if err != nil {
		return err
	}
	_, err = io.Copy(out, in)
	if e := out.Close(); err == nil {
		err = e
	}
	return err
}

func handleClassicPlay(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		w.WriteHeader(405)
		return
	}
	cl.Lock()
	ok := cl.ver != "" && !cl.busy
	cl.Unlock()
	if !ok {
		http.Error(w, "Realm of Aldara Classic is not installed yet.", 409)
		return
	}
	name := ""
	upd.Lock()
	if upd.latest != nil && upd.latest.Classic != nil {
		name = upd.latest.Classic.Exe
	}
	upd.Unlock()
	exe := filepath.Join(classicDir(), exeOr(name))
	if _, err := os.Stat(exe); err != nil {
		// the installed copy may be older than the feed: start the game program that is there
		for _, n := range []string{"Realm of Aldara.exe", "Realm of Aldara Classic.exe"} {
			if _, e := os.Stat(filepath.Join(classicDir(), n)); e == nil {
				exe = filepath.Join(classicDir(), n)
				break
			}
		}
	}
	cmd := exec.Command(exe)
	cmd.Dir = classicDir()
	if err := cmd.Start(); err != nil {
		http.Error(w, "Could not start Realm of Aldara Classic: "+err.Error(), 500)
		return
	}
	go func() {
		time.Sleep(600 * time.Millisecond)
		select {
		case quitCh <- struct{}{}:
		default:
		}
	}()
	w.WriteHeader(204)
}

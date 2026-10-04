//go:build windows

package main

import (
	"os"
	"os/exec"
	"runtime"
	"strings"
	"syscall"
	"time"
	"unsafe"

	webview2 "github.com/jchv/go-webview2"
)

// The game window must live on the process's main thread.
func init() { runtime.LockOSThread() }

var (
	user32            = syscall.NewLazyDLL("user32.dll")
	pGetWindowLongPtr = user32.NewProc("GetWindowLongPtrW")
	pSetWindowLongPtr = user32.NewProc("SetWindowLongPtrW")
	pSetWindowPos     = user32.NewProc("SetWindowPos")
	pGetWindowRect    = user32.NewProc("GetWindowRect")
	pMonitorFromWin   = user32.NewProc("MonitorFromWindow")
	pGetMonitorInfo   = user32.NewProc("GetMonitorInfoW")
	pShowWindow       = user32.NewProc("ShowWindow")
)

type rect struct{ L, T, R, B int32 }
type monitorInfo struct {
	Size          uint32
	Monitor, Work rect
	Flags         uint32
}

const (
	gwlStyle           = ^uintptr(15) // GWL_STYLE (-16)
	wsOverlappedWindow = 0x00CF0000
	swpNoZOrder        = 0x0004
	swpNoOwnerZOrder   = 0x0200
	swpFrameChanged    = 0x0020
	swMaximize         = 3
)

// display mode: "windowed" (normal window with a frame), "borderless" (no frame, fills the screen above the taskbar)
// or "fullscreen" (no frame, covers the whole monitor including the taskbar)
var display struct {
	mode  string
	style uintptr
	r     rect
}

func setDisplay(h uintptr, mode string) {
	if display.mode == "" {
		display.mode = "windowed"
	}
	if mode != "windowed" && mode != "borderless" && mode != "fullscreen" {
		return
	}
	if mode == display.mode {
		return
	}
	if display.mode == "windowed" { // remember the normal window to come back to
		display.style, _, _ = pGetWindowLongPtr.Call(h, gwlStyle)
		pGetWindowRect.Call(h, uintptr(unsafe.Pointer(&display.r)))
	}
	if mode == "windowed" {
		pSetWindowLongPtr.Call(h, gwlStyle, display.style|wsOverlappedWindow)
		r := display.r
		pSetWindowPos.Call(h, 0, uintptr(r.L), uintptr(r.T), uintptr(r.R-r.L), uintptr(r.B-r.T), swpNoZOrder|swpNoOwnerZOrder|swpFrameChanged)
	} else {
		mon, _, _ := pMonitorFromWin.Call(h, 2)
		mi := monitorInfo{Size: uint32(unsafe.Sizeof(monitorInfo{}))}
		pGetMonitorInfo.Call(mon, uintptr(unsafe.Pointer(&mi)))
		a := mi.Monitor
		if mode == "borderless" {
			a = mi.Work
		}
		pSetWindowLongPtr.Call(h, gwlStyle, display.style&^wsOverlappedWindow)
		pSetWindowPos.Call(h, 0, uintptr(a.L), uintptr(a.T), uintptr(a.R-a.L), uintptr(a.B-a.T), swpNoOwnerZOrder|swpFrameChanged)
	}
	display.mode = mode
}

// F11: fullscreen and back to the mode you came from
var lastNonFull = "windowed"

func toggleFullscreen(h uintptr) {
	if display.mode == "fullscreen" {
		setDisplay(h, lastNonFull)
	} else {
		if display.mode != "" {
			lastNonFull = display.mode
		}
		setDisplay(h, "fullscreen")
	}
}

// Runs before the game's own scripts: F11 fullscreen, and switches off the
// web-page shortcuts (reload, print, find, zoom, back/forward...) so the game
// behaves like a desktop program.
const shimJS = `(function(){
window.ALDARA_APP=true;
addEventListener('keydown',function(e){
  var k=e.key;
  if(k==='F11'){ e.preventDefault(); e.stopImmediatePropagation(); if(window.aldaraFullscreen) window.aldaraFullscreen(); return; }
  var ctl=e.ctrlKey||e.metaKey;
  if(k==='F5'||k==='F3'||k==='F7'||k==='F12'||k==='BrowserBack'||k==='BrowserForward'||k==='BrowserRefresh'||
     (e.altKey&&(k==='ArrowLeft'||k==='ArrowRight'||k==='Home'))||
     (ctl&&'rRpPfFgGuUsSjJhHnNtTdDoO+-=0'.indexOf(k)>=0)){ e.preventDefault(); }
},true);
addEventListener('wheel',function(e){ if(e.ctrlKey) e.preventDefault(); },{passive:false,capture:true});
addEventListener('dragstart',function(e){ if(!(e.target&&e.target.draggable)) e.preventDefault(); },true);
var rq=function(){ if(window.aldaraFullscreen) window.aldaraFullscreen(); return Promise.resolve(); };
try{ Element.prototype.requestFullscreen=rq; Document.prototype.exitFullscreen=rq; }catch(e){}
})();`

// runWindow shows the game in its own native window (the WebView2 engine built
// into Windows draws it; no browser is opened). Returns false if it can't.
func runWindow(url, title string) bool {
	args := append([]string{"--disable-features=Translate,msSmartScreenProtection,msEdgeTranslate", "--autoplay-policy=no-user-gesture-required", "--disable-pinch"}, gpuArgs()...)
	os.Setenv("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", strings.Join(args, " "))
	os.Setenv("WEBVIEW2_DEFAULT_BACKGROUND_COLOR", "FF0B0D14")
	restarted := hasArg("--restarted")
	var w webview2.WebView
	for attempt := 0; ; attempt++ {
		w = webview2.NewWithOptions(webview2.WebViewOptions{
			Debug:     false,
			AutoFocus: true,
			DataPath:  profileDir,
			WindowOptions: webview2.WindowOptions{
				Title: title, Width: 1400, Height: 880, IconId: 2, Center: true,
			},
		})
		if w != nil {
			break
		}
		// after a restart the previous engine may still be closing down
		if !restarted || attempt >= 10 {
			return false
		}
		time.Sleep(time.Second)
	}
	hwnd := uintptr(w.Window())
	w.Bind("aldaraFullscreen", func() { w.Dispatch(func() { toggleFullscreen(hwnd) }) })
	w.Bind("aldaraDisplay", func(m string) { w.Dispatch(func() { setDisplay(hwnd, m) }) })
	w.Bind("aldaraDisplayMode", func() string {
		if display.mode == "" {
			return "windowed"
		}
		return display.mode
	})
	w.Bind("aldaraQuit", func() { w.Dispatch(func() { w.Destroy() }) })
	w.Bind("aldaraTitle", func(t string) { w.Dispatch(func() { w.SetTitle(t) }) })
	w.Init(shimJS)
	restart := false
	go func() {
		select {
		case <-restartCh:
			w.Dispatch(func() { restart = true; w.Destroy() })
		case <-quitCh:
			w.Dispatch(func() { w.Destroy() })
		}
	}()
	w.Navigate(url)
	w.Run()
	if restart {
		// start a fresh copy with the new graphics card settings, then quit
		if listener != nil {
			listener.Close()
		}
		if exe, err := os.Executable(); err == nil {
			exec.Command(exe, restartArgs()...).Start()
		}
	}
	return true
}

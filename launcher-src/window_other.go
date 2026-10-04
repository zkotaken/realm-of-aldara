//go:build !windows

package main

// runWindow shows the game in its own native window. Only Windows has one;
// elsewhere the launcher falls back to an app-mode browser window.
func runWindow(url, title string) bool { return false }

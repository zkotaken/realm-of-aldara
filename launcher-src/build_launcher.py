#!/usr/bin/env python3
# Builds launcher.html (fonts from the game, hero picture inlined), writes the release files and the exe.
import re, base64, hashlib, json, os, subprocess, shutil, sys, datetime
D=os.path.dirname(os.path.abspath(__file__)); os.chdir(D)
game=open('game.html',encoding='utf-8').read()
fonts='\n'.join(l for l in game.split('\n')[:40] if l.startswith('@font-face') and ("'Cinzel'" in l or "'Cinzel Decorative'" in l or "'Alegreya'" in l))
src=open('launcher_src.html',encoding='utf-8').read()
hero='data:image/jpeg;base64,'+base64.b64encode(open('hero.jpg','rb').read()).decode()
out=src.replace('/*FONTS*/',fonts).replace('url(HERO)','url('+hero+')')
open('launcher.html','w',encoding='utf-8').write(out)
ver=open('version.txt').read().strip()
env=dict(os.environ,GOFLAGS='-mod=mod',GOPROXY='off',GOSUMDB='off',GOOS='windows',GOARCH='amd64')
subprocess.run(['go','build','-trimpath','-ldflags','-H windowsgui','-o','Realm of Aldara Launcher.exe','.'],check=True,env=env)
# release files for the update server
R='/home/claude/realm-of-aldara'
if os.path.isdir(R):
    b=open('game.html','rb').read()
    shutil.copy('game.html',os.path.join(R,'game.html')); shutil.copy('launcher.html',os.path.join(R,'launcher.html'));
    lb=open('launcher.html','rb').read(); lv=re.search(r'launcherPageVersion = (\d+)',open('updater.go').read()).group(1)
    shutil.copy('patchnotes.json',os.path.join(R,'patchnotes.json')); shutil.copy('Realm of Aldara Launcher.exe',os.path.join(R,'Realm of Aldara Launcher.exe'))
    notes=json.load(open('patchnotes.json')); top=notes['entries'][0]
    json.dump({'version':ver,'size':len(b),'sha256':hashlib.sha256(b).hexdigest(),'file':'game.html','date':top.get('date',''),'title':top.get('title',''),'launcher':lv,'launcher_sha256':hashlib.sha256(lb).hexdigest(),
               'assets':[{'p':'music/'+f,'s':hashlib.sha256(open(os.path.join(R,'music',f),'rb').read()).hexdigest(),'n':os.path.getsize(os.path.join(R,'music',f))} for f in sorted(os.listdir(os.path.join(R,'music')))] if os.path.isdir(os.path.join(R,'music')) else [],
               'exe':'Realm of Aldara Launcher.exe','exe_build':re.search(r'const exeBuild = (\d+)',open('updater.go').read()).group(1),'exe_sha256':hashlib.sha256(open('Realm of Aldara Launcher.exe','rb').read()).hexdigest(),**({'classic':json.load(open('classic.json'))} if os.path.exists('classic.json') else {})},open(os.path.join(R,'version.json'),'w'),indent=1)
print('launcher',len(out),'version',ver)

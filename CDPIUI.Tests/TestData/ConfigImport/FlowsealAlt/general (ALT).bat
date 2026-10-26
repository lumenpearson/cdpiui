@echo off
@rem ALT variant: same layout, other strategy
set "BIN=%~dp0bin\"
set "LISTS=%~dp0lists\"

start "zapret: %~n0" /min "%BIN%winws.exe" --wf-tcp=443 --wf-udp=443 ^
--filter-udp=443 --hostlist="%LISTS%list-general.txt" --dpi-desync=fake --dpi-desync-repeats=11 --dpi-desync-fake-quic="%BIN%quic_initial_www_google_com.bin" --new ^
--filter-tcp=443 --hostlist="%LISTS%list-general.txt" --dpi-desync=fake,disorder2 --dpi-desync-split-pos=1 --wssize=1:6 --dpi-desync-fake-tls="%BIN%tls_clienthello_www_google_com.bin"

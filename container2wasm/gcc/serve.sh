( cd ./examples/emscripten/htdocs/ && npx webpack && cp -R index.html dist vendor/xterm.css /tmp/out-js4/htdocs/ )
wget -O /tmp/c2w-net-proxy.wasm https://github.com/ktock/container2wasm/releases/download/v0.5.0/c2w-net-proxy.wasm
cat /tmp/c2w-net-proxy.wasm | gzip > /tmp/out-js4/htdocs/c2w-net-proxy.wasm.gzip
cp ./examples/emscripten/xterm-pty.conf /tmp/out-js4/
docker run --rm -p 8080:80 \
         -v "/tmp/out-js4/htdocs:/usr/local/apache2/htdocs/:ro" \
         -v "/tmp/out-js4/xterm-pty.conf:/usr/local/apache2/conf/extra/xterm-pty.conf:ro" \
         --entrypoint=/bin/sh httpd -c 'echo "Include conf/extra/xterm-pty.conf" >> /usr/local/apache2/conf/httpd.conf && httpd-foreground'
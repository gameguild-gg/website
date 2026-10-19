"""Loopback-only TLS bridge for the hosted native EVM fixture; never a deployment server."""

import argparse
import http.client
import http.server
import ssl

MAXIMUM_BYTES = 1024 * 1024


class RpcBridge(http.server.BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def do_POST(self):
        if self.path != "/":
            self.send_error(404)
            return
        try:
            length = int(self.headers.get("Content-Length", "-1"))
        except ValueError:
            self.send_error(400)
            return
        if length < 0 or length > MAXIMUM_BYTES:
            self.send_error(413)
            return
        self.connection.settimeout(15)
        body = self.rfile.read(length)
        upstream = http.client.HTTPConnection("127.0.0.1", self.server.upstream_port, timeout=10)
        try:
            upstream.request("POST", "/", body, {"Content-Type": "application/json"})
            response = upstream.getresponse()
            data = response.read(MAXIMUM_BYTES + 1)
            if len(data) > MAXIMUM_BYTES:
                self.send_error(502)
                return
            self.send_response(response.status)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(data)))
            self.send_header("Connection", "close")
            self.end_headers()
            self.wfile.write(data)
            self.close_connection = True
        finally:
            upstream.close()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--certificate", required=True)
    parser.add_argument("--key", required=True)
    parser.add_argument("--port", type=int, default=8546)
    parser.add_argument("--upstream-port", type=int, default=8545)
    args = parser.parse_args()
    server = http.server.ThreadingHTTPServer(("127.0.0.1", args.port), RpcBridge)
    server.upstream_port = args.upstream_port
    context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
    context.minimum_version = ssl.TLSVersion.TLSv1_2
    context.load_cert_chain(args.certificate, args.key)
    server.socket = context.wrap_socket(server.socket, server_side=True)
    with server:
        server.serve_forever()


if __name__ == "__main__":
    main()

from http.server import BaseHTTPRequestHandler, HTTPServer
import urllib.parse

class SimpleHandler(BaseHTTPRequestHandler):
    def do_GET(self):
        parsed_path = urllib.parse.urlparse(self.path)
        print(f"Received GET request:\nPath: {parsed_path.path}\nQuery: {parsed_path.query}")
        self.send_response(200)
        self.end_headers()
        self.wfile.write(b"OK")

server_address = ('', 8000)
httpd = HTTPServer(server_address, SimpleHandler)
print("Server running on port 8000...")
httpd.serve_forever()
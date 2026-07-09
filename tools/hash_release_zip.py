import hashlib
import pathlib
import urllib.request

URL = "https://github.com/chrisflory/AutoFocusGraphs/releases/download/v0.1.0.0/AutoFocusGraphs.0.1.0.0.zip"
CANDIDATES = [
    pathlib.Path(__file__).resolve().parents[1] / "publish" / "AutoFocusGraphs.0.1.0.0.zip",
    pathlib.Path(__file__).resolve().parents[1] / "AutoFocusGraphs.0.1.0.0.zip",
    pathlib.Path.home() / "AppData/Local/Temp/AutoFocusGraphs.0.1.0.0.zip",
]

path = next((p for p in CANDIDATES if p.exists()), None)
if path is None:
    path = pathlib.Path(__file__).resolve().parent / "_release.zip"
    print(f"Downloading {URL} ...")
    urllib.request.urlretrieve(URL, path)

digest = hashlib.sha256(path.read_bytes()).hexdigest()
print(f"file: {path}")
print(f"sha256: {digest}")
print(f"length: {len(digest)}")

old = "6db7f503a3b63dfc339fc067a9b8c99dcd8f4b3007a4b11e59dfcfc8abe6b130a6"
print(f"recorded: {old} (len {len(old)})")
print(f"matches recorded: {digest == old.lower()}")

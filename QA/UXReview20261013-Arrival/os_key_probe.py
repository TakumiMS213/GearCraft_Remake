"""OS event probe for the inspected Unity PID; always releases held keys."""
import ctypes
import ctypes.util
import sys
import time
import subprocess

cg = ctypes.CDLL(ctypes.util.find_library('CoreGraphics'))
cf = ctypes.CDLL(ctypes.util.find_library('CoreFoundation'))
cg.CGPreflightPostEventAccess.restype = ctypes.c_bool
cg.CGEventCreateKeyboardEvent.argtypes = [ctypes.c_void_p, ctypes.c_ushort, ctypes.c_bool]
cg.CGEventCreateKeyboardEvent.restype = ctypes.c_void_p
cg.CGEventPostToPid.argtypes = [ctypes.c_int, ctypes.c_void_p]
cg.CGEventPost.argtypes = [ctypes.c_uint, ctypes.c_void_p]
cf.CFRelease.argtypes = [ctypes.c_void_p]
if not cg.CGPreflightPostEventAccess():
    raise SystemExit('No existing event-post permission')
pid = int(sys.argv[1])
key = int(sys.argv[2])
duration = float(sys.argv[3])
use_hid = len(sys.argv) > 4 and sys.argv[4] == 'hid'
if not 0 < duration <= 2:
    raise SystemExit('Duration must be at most 2 seconds')
if use_hid:
    deadline = time.monotonic() + 3
    while True:
        front = subprocess.check_output(['osascript', '-e', 'tell application "System Events" to get unix id of first application process whose frontmost is true'], text=True).strip()
        if front == str(pid) or time.monotonic() >= deadline:
            break
        time.sleep(0.1)
    if front != str(pid):
        raise SystemExit('Unity is not frontmost; refusing global event: ' + front)

def send(down):
    event = cg.CGEventCreateKeyboardEvent(None, key, down)
    if not event:
        raise RuntimeError('Could not create keyboard event')
    try:
        if use_hid:
            cg.CGEventPost(0, event)
        else:
            cg.CGEventPostToPid(pid, event)
    finally:
        cf.CFRelease(event)

try:
    send(True)
    time.sleep(duration)
finally:
    send(False)
print('Posted key down/up to Unity PID', pid, 'key', key, 'duration', duration, 'HID', use_hid)

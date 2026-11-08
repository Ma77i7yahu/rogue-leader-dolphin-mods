# Finds emulated main memory in a running Dolphin and checks the words the installer's patches write.
# Usage: ramcheck.py <Dolphin pid>
import ctypes as C, ctypes.wintypes as W, struct, sys


class MBI(C.Structure):
    _fields_ = [("BaseAddress", C.c_void_p), ("AllocationBase", C.c_void_p), ("AllocationProtect", W.DWORD),
                ("PartitionId", W.WORD), ("RegionSize", C.c_size_t), ("State", W.DWORD), ("Protect", W.DWORD), ("Type", W.DWORD)]


k = C.WinDLL("kernel32", use_last_error=True)
k.OpenProcess.restype = W.HANDLE
k.OpenProcess.argtypes = [W.DWORD, W.BOOL, W.DWORD]
k.VirtualQueryEx.argtypes = [W.HANDLE, C.c_void_p, C.POINTER(MBI), C.c_size_t]
k.VirtualQueryEx.restype = C.c_size_t
k.ReadProcessMemory.argtypes = [W.HANDLE, C.c_void_p, C.c_void_p, C.c_size_t, C.POINTER(C.c_size_t)]
h = k.OpenProcess(0x0410, False, int(sys.argv[1]))
if not h:
    sys.exit("can't open the process")


def read(addr, n):
    b = C.create_string_buffer(n)
    got = C.c_size_t()
    return b.raw[:got.value] if k.ReadProcessMemory(h, addr, b, n, C.byref(got)) else b""


# word -> what proves the option's patch applied; offsets are physical (0x80001000 -> 0x1000)
WORDS = {
    0x0A00: (0x3D808000, "widescreen camera cave"),
    0x0AF0: (0x3FAF8AF9, "widescreen aspect constant"),
    0x0AA0: (0x819D00B4, "terrain texture check 1"),
    0x0AC0: (0x819F00B4, "terrain texture check 2"),
    0x1100: (0x819E0484, "linear turn cave"),
    0x1140: (0x819E0484, "linear pitch cave"),
    0x0A80: (0xA07C0000, "tight turn C-stick cave"),
    0x1180: (0x3D808000, "laser convergence cave"),
    0x1230: (0x3E32B8C2, "convergence limit"),
    0x1000: (0x2C1A0004, "Needed for Gold cave"),
    0x1280: (0x81060008, "wingman kills cave"),
}
SIZE_48MB = 0x3000000
addr, hits = 0, []
mbi = MBI()
while k.VirtualQueryEx(h, addr, C.byref(mbi), C.sizeof(mbi)):
    base = mbi.BaseAddress or 0
    if mbi.State == 0x1000 and mbi.Protect & 0xEE and not mbi.Protect & 0x100 and read(base, 6) == b"GSWE64":
        hits.append(base)
    addr = base + mbi.RegionSize
    if addr >= 0x7FFFFFFFFFFF:
        break
if not hits:
    sys.exit("emulated memory not found")
fails = 0
base = hits[0]
print("emulated memory copies at", [hex(x) for x in hits])
for off, (want, what) in sorted(WORDS.items()):
    raw = read(base + off, 4)
    got = struct.unpack(">I", raw)[0] if len(raw) == 4 else None
    ok = got == want
    fails += not ok
    print("PASS" if ok else "FAIL", hex(0x80000000 + off), what, "=", hex(got) if got is not None else "unreadable")
# the 48 MB setting: the last byte below 48 MB is readable, the 24 MB stock size wouldn't be
over = read(base + SIZE_48MB - 16, 16)
print("PASS" if len(over) == 16 else "FAIL", "memory readable up to 48 MB")
fails += len(over) != 16
print("ALL PASS" if not fails else "%d FAILED" % fails)
sys.exit(1 if fails else 0)

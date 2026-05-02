import sys
import time

print("Hello from Python")
if len(sys.argv) > 1:
    print("Arguments:", " ".join(sys.argv[1:]))

time.sleep(1)
print("Done.")

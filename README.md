# FreeBlock

FreeBlock is a free and open source CLI website blocker for Linux, macOS and Windows

## Usage

- `freeblock status`: Show the current status of block lists (green means active).
- `freeblock list add`: Create a new block list. Add one website to block per line in the file that will open.
- `freeblock list edit`: Edit the websites of a block list. You won't be able to remove websites while the list is active.
- `freeblock list rename`: Rename a block list.
- `freeblock list remove`: Remove a block list. This function is disabled if the list is blocked.
- `freeblock block`: Enables manual block for a list.
- `freeblock unblock`: Disables manual block for a list.
- `freeblock lock`: Locks a list for the provided amount of time. You won't be able to disable it until the timer ends.

## Roadmap

- [x] Timers
- [x] Editing lists
- [ ] Schedules
- [ ] Breaks
- [ ] Preventing workarounds
- [ ] Blocking apps

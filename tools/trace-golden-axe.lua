-- Code authored by Dean Edis (DeanTheCoder).
-- Anyone is free to copy, modify, use, compile, or distribute this software,
-- either in source code form or as a compiled binary, for any purpose.
--
-- If you modify the code, please retain this copyright header,
-- and consider contributing back to the repository or letting us know
-- about your modifications. Your contributions are valued!
--
-- THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

-- Read-only arcade combat trace for the US set 6 (MAME goldnaxe).
-- GA_TRACE=solo.csv mame goldnaxe -rompath /path/to/roms -autoboot_script tools/trace-golden-axe.lua
-- Play normally or load a saved fight. Repeat with the same difficulty and two players.
-- Use base_x/base_z for movement speed. The displayed coordinates can include pose offsets.
-- Object layout: https://github.com/atomic14/golden-axe#technical-notes

assert(manager.machine.system.name == "goldnaxe", "This trace expects the US set 6 arcade program.")
local memory = manager.machine.devices[":maincpu"].spaces["program"]
local output = assert(io.open(assert(os.getenv("GA_TRACE"), "Set GA_TRACE to the output CSV path."), "w"))
output:write("tick,area,progress,slot,flags,routine,palette,animation,sprite,hp,x,z,y,base_x,base_z,base_y\n")

local tick = 0
local function trace_frame()
    tick = tick + 1
    local area = memory:read_u8(0xffed4a)
    local progress = memory:read_u16(0xffed32)
    for slot = 0xffc000, 0xffdf80, 0x80 do
        local flags = memory:read_u16(slot)
        if flags & 0x8000 ~= 0 then
            output:write(string.format("%d,%d,%d,%06x,%04x,%06x,%02x,%06x,%04x,%d,%d,%d,%d,%.4f,%.4f,%.4f\n",
                tick, area, progress, slot, flags, memory:read_u32(slot + 2), memory:read_u8(slot + 0xb),
                memory:read_u32(slot + 0x28), memory:read_u16(slot + 6), memory:read_u8(slot + 0x70),
                memory:read_u16(slot + 0x10), memory:read_u16(slot + 0x14), memory:read_u16(slot + 0x18),
                memory:read_u32(slot + 0x2c) / 65536, memory:read_u32(slot + 0x30) / 65536,
                memory:read_u32(slot + 0x34) / 65536))
        end
    end
    if tick % 60 == 0 then
        output:flush()
    end
end

-- Keep the subscriptions alive for the duration of the emulation session.
ga_trace_frame = emu.add_machine_frame_notifier(trace_frame)
ga_trace_stop = emu.add_machine_stop_notifier(function() output:close() end)

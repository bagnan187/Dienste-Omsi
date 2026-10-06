-- openOMSI -> lokaler ROGIS Depot Sync
local PORT = 47830

local function esc(s)
  s = tostring(s or "")
  s = s:gsub("\\", "\\\\"):gsub('"', '\\"'):gsub("\n", "\\n")
  return s
end

local function send_state(reason)
  local i = omsi.info()
  local date = os.date("%Y-%m-%d")
  local msg = string.format(
    '{"type":"state","reason":"%s","date":"%s","clock":"%s","map":"%s","line":"%s","tour":"%s","vehicle":"%s"}',
    esc(reason), esc(date), esc(omsi.clock()), esc(i.map), esc(i.line), esc(i.tour), esc(omsi.vehicle())
  )
  omsi.send(PORT, msg)
end

omsi.on("start", function() send_state("start") end)
omsi.on("vehicle", function() send_state("vehicle") end)
omsi.on("duty", function() send_state("duty") end)
omsi.every(30, function() send_state("heartbeat") end)

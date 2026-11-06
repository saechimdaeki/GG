extends Node
## Finding each other without Steam:
## - LAN: the host shouts "HM|<port>" on UDP broadcast once a second; the menu listens
##   and lists every host it hears.
## - WAN: the host asks the router (UPnP) to forward the game port and reports the
##   public address to share. Runs in a thread: router discovery blocks for ~2 s.
## ponytail: no relay/NAT punch-through — a router without UPnP needs a manual port forward.

const DISCOVERY_PORT := 7778
const TAG := "HM|"

## Emitted on the main thread: public "ip:port", or "" with a reason when UPnP failed.
signal wan_ready(address: String, error: String)

## LAN hosts heard recently: "ip:port" -> msec last heard.
var found := {}
var _beacon := PacketPeerUDP.new()
var _listener := PacketPeerUDP.new()
var _game_port := 0
var _beacon_t := 0.0
var _upnp: UPNP
var _thread: Thread


## "1.2.3.4" -> ["1.2.3.4", default_port]; "1.2.3.4:9000" -> ["1.2.3.4", 9000].
static func parse_address(text: String, default_port: int) -> Array:
	var t := text.strip_edges()
	var i := t.rfind(":")
	if i > 0 and t.substr(i + 1).is_valid_int():
		return [t.substr(0, i), t.substr(i + 1).to_int()]
	return [t if t != "" else "127.0.0.1", default_port]


## This machine's LAN IPv4 addresses (what a friend on the same Wi-Fi would type).
static func local_ips() -> Array:
	var out := []
	for a in IP.get_local_addresses():
		if a.count(".") == 3 and not a.begins_with("127.") and not a.begins_with("169.254."):
			out.append(a)
	return out


## Host: start shouting on the LAN.
func start_beacon(game_port: int) -> void:
	_game_port = game_port
	_beacon.set_broadcast_enabled(true)
	_beacon.set_dest_address("255.255.255.255", DISCOVERY_PORT)


## Menu: start listening for hosts.
func start_listening() -> int:
	return _listener.bind(DISCOVERY_PORT)


func stop_listening() -> void:
	_listener.close()


## Host: open the port on the router in a background thread; wan_ready fires when done.
func open_wan(game_port: int) -> void:
	_thread = Thread.new()
	_thread.start(_upnp_work.bind(game_port))


func _upnp_work(game_port: int) -> void:
	var u := UPNP.new()
	var err := u.discover(2000, 2)
	if err != UPNP.UPNP_RESULT_SUCCESS or u.get_gateway() == null or not u.get_gateway().is_valid_gateway():
		wan_ready.emit.call_deferred("", "공유기에서 UPnP를 못 찾음 (공유기 설정에서 포트 %d UDP 직접 열기)" % game_port)
		return
	if u.add_port_mapping(game_port, game_port, "HauntedMovers", "UDP") != UPNP.UPNP_RESULT_SUCCESS:
		wan_ready.emit.call_deferred("", "공유기가 포트 열기를 거부함 (포트 %d UDP 직접 열기)" % game_port)
		return
	_upnp = u
	wan_ready.emit.call_deferred("%s:%d" % [u.query_external_address(), game_port], "")


func _process(delta: float) -> void:
	if _game_port > 0:
		_beacon_t -= delta
		if _beacon_t <= 0.0:
			_beacon_t = 1.0
			_beacon.put_packet((TAG + str(_game_port)).to_utf8_buffer())
	if _listener.is_bound():
		while _listener.get_available_packet_count() > 0:
			var msg := _listener.get_packet().get_string_from_utf8()
			if msg.begins_with(TAG):
				found["%s:%s" % [_listener.get_packet_ip(), msg.trim_prefix(TAG)]] = Time.get_ticks_msec()
		for k in found.keys():
			if Time.get_ticks_msec() - found[k] > 4000:
				found.erase(k)


func _exit_tree() -> void:
	if _thread:
		_thread.wait_to_finish()
	if _upnp:
		_upnp.delete_port_mapping(_game_port, "UDP")
	_beacon.close()
	_listener.close()

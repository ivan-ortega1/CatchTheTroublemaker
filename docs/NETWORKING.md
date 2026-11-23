# Playing across machines and networks

The Unity client only ever needs one setting: **Server URL** on the home screen.
What you put there depends on where the Node server runs.

`localhost` always means **the device running the client**. It is correct only when
the Node server runs on that same device.

```
Clients --HTTP/SSE--> http://<host>:3000
```

## Automatic LAN discovery (same network)

The server listens on **UDP 3001** and answers broadcast probes from Unity clients.
On a client in the same network you do **not** need to type anything:

- On startup, if the saved URL is `localhost` and that is not reachable, the client
  automatically searches the LAN, finds the host and fills in **Server URL**.
- You can also press **FIND LAN** on the Home screen at any time. It says
  `Found: http://…:3000` when the host answers.

The Windows firewall rule installed with Node.js allows both TCP (3000) and UDP
(3001), so no manual rule is normally needed. If discovery finds nothing, check that
the host server is running and that the machines are on the same Wi-Fi (guest Wi-Fi
and client isolation block broadcasts).

Start the server on the host with:

```bash
node server/index.js
```

It listens on all interfaces (`::`) and prints the addresses you can share:

```
Catch the Troublemaker -> http://localhost:3000
  SHARE THIS: http://192.168.0.253:3000   (same Wi-Fi / LAN)
  LAN: http://10.0.0.5:3000   (same Wi-Fi only)
Players on mobile data / another network need a public tunnel URL or a cloud host.
```

## 1. Same device as the server

Use the default `http://localhost:3000`.

## 2. Different machine, same home network

1. Connect both machines to the **same Wi-Fi/LAN** (the second machine must **not** be on mobile data).
2. On the host, copy the `SHARE THIS` URL printed by the server.
3. On the other machine, paste it into **Server URL** in Unity and press **TEST**.
   It should say **Server OK**.
4. Allow Node.js through the host firewall. On Windows the installer usually creates
   an inbound *Node.js JavaScript Runtime* rule; otherwise allow TCP port 3000.

If `TEST` fails, check: server still running, both devices on the same subnet,
firewall rule, and that you typed `http://<ip>:3000` with no trailing path.

## 3. Different networks (mobile data, another house, internet)

A LAN address cannot be reached from another network. You need a **public URL** that
forwards to the host's port 3000. Pick one:

### Option A — Cloudflare Tunnel (no port forwarding, free)

```bash
# install cloudflared once, then:
cloudflared tunnel --url http://localhost:3000
```

It prints an `https://<random>.trycloudflare.com` URL. Paste that into **Server URL**
on every client. The tunnel process must stay running.

### Option B — ngrok

```bash
ngrok http 3000
```

Use the printed `https://...` forwarding URL.

### Option C — Host the server in the cloud

Deploy `server/` to Render, Railway, Fly.io, etc. as a Node service:

- start command: `node server/index.js`
- bind to `0.0.0.0` (the Node default) and respect the `PORT` environment variable
  (already implemented).

Then use the deployment's public URL in every client. This is the most reliable
option for a game night with people on different networks.

> Note: browsers must also allow the public URL. The Node server sends permissive
> CORS headers, and Unity's `UnityWebRequest` works with the `https://` URLs provided
> by these tunnels/cloud hosts.

## Changing servers while playing

If you switch networks while a room is open, use **Leave → Home**, set the new Server
URL, press **TEST**, then create or join again. The lobby keeps polling every 350 ms,
so it recovers by itself once the host is reachable again.

## Quick checklist

| Symptom | Most likely cause |
| --- | --- |
| `Cannot reach http://localhost:3000` on a second machine | You left `localhost`; use the host's LAN/public URL |
| `Cannot reach http://192.168.x.x:3000` on mobile data | LAN IP is not routable from mobile data; use a tunnel/cloud URL |
| `TEST` fails on the host too | Server not running, or wrong port |
| `TEST` works on host but not on another LAN machine | Firewall blocking port 3000, or different Wi-Fi/subnet |
| Connects, then lobby freezes after switching networks | Change Server URL via Leave → Home and TEST again |

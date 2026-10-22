# Task 6 (Optional): DNS as a Distributed System

## Trace status

The command required by the lab is:

    dig +trace www.example.com

It was run from the lab sandbox, but the sandbox DNS path did not return a real
resolution (see `dns_probe_output.txt`). The recursive resolver 8.8.8.8 and the
root server 198.41.0.4 both answered `NXDOMAIN` for `www.example.com`, which a
working root server never does, so the sandbox network is intercepting DNS.
The root, TLD and authoritative rows of the trace table are therefore left for
a run on a machine with normal DNS access. Run `dig +trace www.example.com > trace.txt`
there and fill in the table from that output.

## How DNS maps onto the architecture styles

- **Client-server.** A resolver (the client) asks a server for an answer. Each
  level of the hierarchy is a server that only answers for its own zone: the
  root knows which servers handle each top-level domain, a TLD server knows the
  authoritative servers of its domains, and the authoritative server holds the
  final record.
- **Peer-to-peer.** The name space is distributed over many servers with no
  single owner of all names, and the caches of resolvers and servers share
  answers with each other. Any resolver can reach the same servers as any other.
- **Layered / hierarchical.** The name space is a tree of zones. Each layer
  (root, TLD, zone) delegates the next layer down, and the resolver walks the
  layers from the top.

Replication (many root and TLD servers) and caching (the TTL on each answer)
let DNS scale and survive the failure of individual servers. The TTL tells a
cache how long it may keep an answer before asking again.

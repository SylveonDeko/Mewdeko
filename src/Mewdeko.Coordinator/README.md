# Coordinator project

Grpc-based coordinator useful for sharded Mewdeko. Its purpose is controlling the lifetime and checking status of the shards it creates.


### Supports
- Checking status
- Individual shard restarts
- Full shard restarts
- Graceful coordinator restarts (restart/update coordinator without killing shards)
- Kill/Stop
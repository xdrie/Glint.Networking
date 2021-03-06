using System.Linq;
using Glint.Networking.Game;
using Glint.Networking.Messages;
using Glint.Networking.Messages.Types;
using Glint.Networking.Pipeline.Messages;
using Glint.Networking.Utils;

namespace Glint.Networking.Pipeline.Relays {
    public abstract class BodyUpdateRelay<TMessage> : ServerMessageRelay<TMessage> where TMessage : BodyUpdateMessage {
        public BodyUpdateRelay(GlintNetServerContext context) : base(context) { }

        protected override bool validate(TMessage msg) {
            // validation logic for body updates?
            return true;
        }
    }

    public class BodyKinematicUpdateRelay : BodyUpdateRelay<BodyKinematicUpdateMessage> {
        public BodyKinematicUpdateRelay(GlintNetServerContext context) : base(context) { }

        protected override ProcessResult process(BodyKinematicUpdateMessage msg) {
            var player = context.clients.SingleOrDefault(x => x.uid == msg.sourceUid);
            if (player == null) {
                Global.log.trace($"kinematic update was for body with unknown owner: {msg} (from {msg.sourceUid})");
                return ProcessResult.Fail;
            }

            var now = NetworkTime.time();

            var userBodies = context.scene.bodies[player];
            var body = userBodies.SingleOrDefault(x => x.id == msg.bodyId);
            if (body == null) {
                Global.log.trace($"kinematic update was for body with unknown body id ({msg.bodyId}): {msg} (from {msg.sourceUid})");
                return ProcessResult.Fail;
            }

            body.lastReceivedTime = now;
            body.lastSnapshotTime = msg.time;
            body.pos = msg.pos;
            body.vel = msg.vel;
            body.angle = msg.angle;
            body.angularVelocity = msg.angularVelocity;

            return ProcessResult.Relay;
        }
    }

    public class BodyLifetimeUpdateRelay : BodyUpdateRelay<BodyLifetimeUpdateMessage> {
        public BodyLifetimeUpdateRelay(GlintNetServerContext context) : base(context) { }

        protected override ProcessResult process(BodyLifetimeUpdateMessage msg) {
            var player = context.clients.SingleOrDefault(x => x.uid == msg.sourceUid);
            if (player == null) {
                Global.log.trace($"lifetime update was for body with unknown owner: {msg} (from {msg.sourceUid})");
                return ProcessResult.Fail;
            }
            var now = NetworkTime.time();
            if (msg.exists) {
                // create
                // TODO: assert not exists
                var userBodies = context.scene.bodies[player];
                if (userBodies.Any(x => x.id == msg.bodyId)) {
                    Global.log.trace($"lifetime update (create) was for a body ({msg.bodyId}) that already exists: {msg} (from {msg.sourceUid})");
                    return ProcessResult.Fail;
                }
                userBodies.Add(new NetScene.Body(msg.sourceUid, msg.time, now, msg.bodyId, msg.syncTag,
                    new PackedVec2(0, 0),
                    new PackedVec2(0, 0), 0, 0));
            }
            else {
                // remove
                // TODO: assert exists
                var userBodies = context.scene.bodies[player];
                var body = userBodies.SingleOrDefault(x => x.id == msg.bodyId);
                if (body == null) {
                    Global.log.trace($"lifetime update (destroy) was for a body ({msg.bodyId}) that doesn't exist: {msg} (from {msg.sourceUid})");
                    return ProcessResult.Fail;
                }
                userBodies.Remove(body);
            }

            return ProcessResult.Relay;
        }
    }
}
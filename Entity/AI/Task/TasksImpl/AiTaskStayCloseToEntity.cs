﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace Vintagestory.GameContent
{
    public class AiTaskStayCloseToEntity : AiTaskBase
    {
        protected Entity targetEntity;
        protected float moveSpeed = 0.03f;
        protected float range = 8f;
        protected float maxDistance = 3f;
        protected string entityCode;
        protected bool stuck = false;
        protected bool onlyIfLowerId = false;
        protected bool allowTeleport;
        protected float teleportAfterRange;

        protected Vec3d targetOffset = new Vec3d();

        public AiTaskStayCloseToEntity(EntityAgent entity) : base(entity)
        {
        }

        public override void LoadConfig(JsonObject taskConfig, JsonObject aiConfig)
        {
            base.LoadConfig(taskConfig, aiConfig);

            moveSpeed = taskConfig["movespeed"].AsFloat(0.03f);
            range = taskConfig["searchRange"].AsFloat(8f);
            maxDistance = taskConfig["maxDistance"].AsFloat(3f);
            onlyIfLowerId = taskConfig["onlyIfLowerId"].AsBool();
            entityCode = taskConfig["entityCode"].AsString();
            allowTeleport = taskConfig["allowTeleport"].AsBool();
            teleportAfterRange = taskConfig["teleportAfterRange"].AsFloat(30f);
        }


        public override bool ShouldExecute()
        {
            if (rand.NextDouble() > 0.01f) return false;

            if (targetEntity == null || !targetEntity.Alive)
            {
                targetEntity = entity.World.GetNearestEntity(entity.ServerPos.XYZ, range, 2, (e) => {
                    return e.Code.Path.Equals(entityCode) && (!onlyIfLowerId || e.EntityId < entity.EntityId);
                });
            }

            if (targetEntity != null && (!targetEntity.Alive || targetEntity.ShouldDespawn)) targetEntity = null;
            if (targetEntity == null) return false;

            double x = targetEntity.ServerPos.X;
            double y = targetEntity.ServerPos.Y;
            double z = targetEntity.ServerPos.Z;

            double dist = entity.ServerPos.SquareDistanceTo(x, y, z);

            return dist > maxDistance * maxDistance;
        }


        public override void StartExecute()
        {
            base.StartExecute();

            float size = targetEntity.SelectionBox.XSize;

            pathTraverser.NavigateTo(targetEntity.ServerPos.XYZ, moveSpeed, size + 0.2f, OnGoalReached, OnStuck, false, 1000, true);

            targetOffset.Set(entity.World.Rand.NextDouble() * 2 - 1, 0, entity.World.Rand.NextDouble() * 2 - 1);

            stuck = false;
        }


        public override bool ContinueExecute(float dt)
        {
            double x = targetEntity.ServerPos.X + targetOffset.X;
            double y = targetEntity.ServerPos.Y;
            double z = targetEntity.ServerPos.Z + targetOffset.Z;

            pathTraverser.CurrentTarget.X = x;
            pathTraverser.CurrentTarget.Y = y;
            pathTraverser.CurrentTarget.Z = z;

            float dist = entity.ServerPos.SquareDistanceTo(x, y, z);

            if (dist < 3 * 3)
            {
                pathTraverser.Stop();
                return false;
            }

            if (allowTeleport && dist > teleportAfterRange * teleportAfterRange && entity.World.Rand.NextDouble() < 0.05)
            {
                tryTeleport();
            }

            return !stuck && pathTraverser.Active;
        }

        private Vec3d findDecentTeleportPos()
        {
            var ba = entity.World.BlockAccessor;
            var rnd = entity.World.Rand;

            Vec3d pos = new Vec3d();
            BlockPos bpos = new BlockPos();
            for (int i = 0; i < 20; i++)
            {
                double rndx = rnd.NextDouble() * 10 - 5;
                double rndz = rnd.NextDouble() * 10 - 5;
                pos.Set(targetEntity.ServerPos.X + rndx, targetEntity.ServerPos.Y, targetEntity.ServerPos.Z + rndz);

                for (int j = 0; j < 8; j++)
                {
                    // Produces: 0, -1, 1, -2, 2, -3, 3
                    int dy = (1 - (j % 2) * 2) * (int)Math.Ceiling(j / 2f);

                    bpos.Set((int)pos.X, (int)(pos.Y + dy + 0.5), (int)pos.Z);
                    Block aboveBlock = ba.GetBlock(bpos);
                    var boxes = aboveBlock.GetCollisionBoxes(ba, bpos);
                    if (boxes != null && boxes.Length > 0) continue;

                    bpos.Set((int)pos.X, (int)(pos.Y + dy - 0.1), (int)pos.Z);
                    Block belowBlock = ba.GetBlock(bpos);
                    boxes = belowBlock.GetCollisionBoxes(ba, bpos);
                    if (boxes == null || boxes.Length == 0) continue;

                    return pos;
                }

            }

            return null;
        }


        protected void tryTeleport()
        {
            if (!allowTeleport) return;
            Vec3d pos = findDecentTeleportPos();
            if (pos != null) entity.TeleportTo(pos);
        }


        public override void FinishExecute(bool cancelled)
        {
            base.FinishExecute(cancelled);
        }

        protected void OnStuck()
        {
            stuck = true;
            tryTeleport();
        }

        public override void OnNoPath(Vec3d target)
        {
            tryTeleport();
        }

        protected void OnGoalReached()
        {
        }
    }
}

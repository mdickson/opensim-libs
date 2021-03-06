/*
 * C# / XNA  port of Bullet (c) 2011 Mark Neale <xexuxjy@hotmail.com>
 *
 * Bullet Continuous Collision Detection and Physics Library
 * Copyright (c) 2003-2008 Erwin Coumans  http://www.bulletphysics.com/
 *
 * This software is provided 'as-is', without any express or implied warranty.
 * In no event will the authors be held liable for any damages arising from
 * the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose, 
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would be
 *    appreciated but is not required.
 * 2. Altered source versions must be plainly marked as such, and must not be
 *    misrepresented as being the original software.
 * 3. This notice may not be removed or altered from any source distribution.
 */

using System;
using BulletXNA.LinearMath;

namespace BulletXNA.BulletCollision
{
    public interface IBroadphaseInterface
    {

        BroadphaseProxy CreateProxy(IndexedVector3 aabbMin, IndexedVector3 aabbMax, BroadphaseNativeTypes shapeType, Object userPtr, CollisionFilterGroups collisionFilterGroup, CollisionFilterGroups collisionFilterMask, IDispatcher dispatcher, Object multiSapProxy);
        BroadphaseProxy CreateProxy(ref IndexedVector3 aabbMin, ref IndexedVector3 aabbMax, BroadphaseNativeTypes shapeType, Object userPtr, CollisionFilterGroups collisionFilterGroup, CollisionFilterGroups collisionFilterMask, IDispatcher dispatcher, Object multiSapProxy);

        void DestroyProxy(BroadphaseProxy proxy, IDispatcher dispatcher);
        void SetAabb(BroadphaseProxy proxy, ref IndexedVector3 aabbMin, ref IndexedVector3 aabbMax, IDispatcher dispatcher);
        void GetAabb(BroadphaseProxy proxy, out IndexedVector3 aabbMin, out IndexedVector3 aabbMax);

        void RayTest(ref IndexedVector3 rayFrom, ref IndexedVector3 rayTo, BroadphaseRayCallback rayCallback);
        void RayTest(ref IndexedVector3 rayFrom, ref IndexedVector3 rayTo, BroadphaseRayCallback rayCallback, ref IndexedVector3 aabbMin, ref IndexedVector3 aabbMax);
        void AabbTest(ref IndexedVector3 aabbMin, ref IndexedVector3 aabbMax, IBroadphaseAabbCallback callback);

        ///calculateOverlappingPairs is optional: incremental algorithms (sweep and prune) might do it during the set aabb
        void CalculateOverlappingPairs(IDispatcher dispatcher);

        IOverlappingPairCache GetOverlappingPairCache();

        ///getAabb returns the axis aligned bounding box in the 'global' coordinate frame
        ///will add some transform later
        void GetBroadphaseAabb(out IndexedVector3 aabbMin, out IndexedVector3 aabbMax);

        ///reset broadphase internal structures, to ensure determinism/reproducability
        void ResetPool(IDispatcher dispatcher);

        void PrintStats();

        void Cleanup();
    }


    public interface IBroadphaseAabbCallback
    {
        void Cleanup();
        bool Process(BroadphaseProxy proxy);
    }


    public abstract class BroadphaseRayCallback : IBroadphaseAabbCallback
    {
        ///added some cached data to accelerate ray-AABB tests
        public IndexedVector3 m_rayDirectionInverse;
        public bool[] m_signs = new bool[3];
        public float m_lambda_max;
        public virtual void Cleanup() { }
        public abstract bool Process(BroadphaseProxy proxy);
    }
}

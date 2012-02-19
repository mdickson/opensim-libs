/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyrightD
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#pragma once

#ifndef BULLET_SIM_H
#define BULLET_SIM_H

#include "BulletCollision/CollisionDispatch/btGhostObject.h"
#include "BulletCollision/CollisionShapes/btHeightfieldTerrainShape.h"
#include "BulletCollision/Gimpact/btGImpactShape.h"
#include "BulletCollision/Gimpact/btGImpactCollisionAlgorithm.h"
#include "LinearMath/btAlignedObjectArray.h"
#include "LinearMath/btMotionState.h"
#include "btBulletDynamicsCommon.h"

#include <map>

// define types that are always 32bits (don't change on 64 bit systems)
#ifdef _MSC_VER
typedef signed __int32		int32_t;
typedef unsigned __int32	uint32_t;
#else
typedef signed int			int32_t;
typedef unsigned int		uint32_t;
#endif

#define IDTYPE uint32_t;

#ifdef __x86_64__
// 64bit systems don't allow you to cast directly from a void* to an unsigned int
#define CONVLOCALID(xx) (unsigned int)((unsigned long)(xx))
#else
#define CONVLOCALID(xx) (unsigned int)(xx)
#endif

#define ID_TERRAIN 0	// OpenSimulator identifies collisions with terrain by localID of zero
#define ID_GROUND_PLANE 1
#define ID_INVALID_HIT 0xFFFFFFFF

// #define TOLERANCE 0.00001
// these values match the ones in SceneObjectPart.SendScheduledUpdates()
#define POSITION_TOLERANCE 0.05f
#define VELOCITY_TOLERANCE 0.001f
#define ROTATION_TOLERANCE 0.01f
#define ANGULARVELOCITY_TOLERANCE 0.01f

// TODO: find a way to build this
static char BulletSimVersionString[] = "v0001 svn126";

// Helper method to determine if an object is phantom or not
static bool IsPhantom(const btCollisionObject* obj)
{
	// Characters are never phantom, but everything else with CF_NO_CONTACT_RESPONSE is
	// TODO: figure out of this assumption for phantom sensing is still true
	return obj->getCollisionShape()->getShapeType() != CAPSULE_SHAPE_PROXYTYPE &&
		(obj->getCollisionFlags() & btCollisionObject::CF_NO_CONTACT_RESPONSE) != 0;
};


// API-exposed structure for a 3D vector
struct Vector3
{
	float X;
	float Y;
	float Z;

	Vector3()
	{
		X = 0.0;
		Y = 0.0;
		Z = 0.0;
	}

	Vector3(float x, float y, float z)
	{
		X = x;
		Y = y;
		Z = z;
	}

	bool AlmostEqual(const Vector3& v, const float nEpsilon)
	{
		return
			(((v.X - nEpsilon) < X) && (X < (v.X + nEpsilon))) &&
			(((v.Y - nEpsilon) < Y) && (Y < (v.Y + nEpsilon))) &&
			(((v.Z - nEpsilon) < Z) && (Z < (v.Z + nEpsilon)));
	}

	btVector3 GetBtVector3()
	{
		return btVector3(X, Y, Z);
	}

	void operator= (const btVector3& v)
	{
		X = v.getX();
		Y = v.getY();
		Z = v.getZ();
	}

	bool operator==(const Vector3& b)
	{
		return (X == b.X && Y == b.Y && Z == b.Z);
	}
};

// API-exposed structure for a rotation
struct Quaternion
{
	float X;
	float Y;
	float Z;
	float W;

	bool AlmostEqual(const Quaternion& q, float nEpsilon)
	{
		return
			(((q.X - nEpsilon) < X) && (X < (q.X + nEpsilon))) &&
			(((q.Y - nEpsilon) < Y) && (Y < (q.Y + nEpsilon))) &&
			(((q.Z - nEpsilon) < Z) && (Z < (q.Z + nEpsilon))) &&
			(((q.W - nEpsilon) < W) && (W < (q.W + nEpsilon)));
	}

	btQuaternion GetBtQuaternion()
	{
		return btQuaternion(X, Y, Z, W);
	}

	void operator= (const btQuaternion& q)
	{
		X = q.getX();
		Y = q.getY();
		Z = q.getZ();
		W = q.getW();
	}
};

// API-exposed structure defining an object
struct ShapeData
{
	enum PhysicsShapeType
	{
		SHAPE_AVATAR = 0,
		SHAPE_BOX = 1,
		SHAPE_CONE = 2,
		SHAPE_CYLINDER = 3,
		SHAPE_SPHERE = 4,
		SHAPE_MESH = 5,
		SHAPE_HULL = 6
	};

	// note that bool's are passed as int's since bool size changes by language
	IDTYPE ID;
	PhysicsShapeType Type;
	Vector3 Position;
	Quaternion Rotation;
	Vector3 Velocity;
	Vector3 Scale;
	float Mass;
	float Buoyancy;		// gravity effect on the object
	unsigned long long HullKey;
	unsigned long long MeshKey;
	float Friction;
	float Restitution;
	int32_t Collidable;	// things can collide with this object
	int32_t Static;	// object is non-moving. Otherwise gravity, etc
};

// API-exposed structure for reporting a collision
struct CollisionDesc
{
	IDTYPE aID;
	IDTYPE bID;
	Vector3 point;
	Vector3 normal;
};

// API-exposed structure to input a convex hull
struct ConvexHull
{
	Vector3 Offset;
	uint32_t VertexCount;
	Vector3* Vertices;
};

// API-exposed structured to return a raycast result
struct RaycastHit
{
	IDTYPE ID;
	float Fraction;
	Vector3 Normal;
};

// API-exposed structure to return a convex sweep result
struct SweepHit
{
	IDTYPE ID;
	float Fraction;
	Vector3 Normal;
	Vector3 Point;
};

// API-exposed structure to return physics updates from Bullet
struct EntityProperties
{
	IDTYPE ID;
	Vector3 Position;
	Quaternion Rotation;
	Vector3 Velocity;
	Vector3 Acceleration;
	Vector3 AngularVelocity;

	EntityProperties(unsigned int id, const btTransform& startTransform)
	{
		ID = id;
		Position = startTransform.getOrigin();
		Rotation = startTransform.getRotation();
	}

	void operator= (const EntityProperties& e)
	{
		ID = e.ID;
		Position = e.Position;
		Rotation = e.Rotation;
		Velocity = e.Velocity;
		Acceleration = e.Acceleration;
		AngularVelocity = e.AngularVelocity;
	}
};

// Block of parameters passed from the managed code.
// The memory layout MUST MATCH the layout in the managed code.
// Rely on the fact that 'float' is always 32 bits in both C# and C++
struct ParamBlock
{
    float defaultFriction;
    float defaultDensity;
	float defaultRestitution;
    float collisionMargin;
    float gravity;

    float linearDamping;
    float angularDamping;
    float deactivationTime;
    float linearSleepingThreshold;
    float angularSleepingThreshold;
    float ccdMotionThreshold;
    float ccdSweptSphereRadius;
    float contactProcessingThreshold;

    float terrainFriction;
    float terrainHitFraction;
    float terrainRestitution;
    float avatarFriction;
    float avatarDensity;
    float avatarRestitution;
    float avatarCapsuleRadius;
    float avatarCapsuleHeight;
};

// ============================================================================================
// Callback to managed code for logging
typedef void DebugLogCallback(const char*);
extern DebugLogCallback* debugLogCallback;
extern void BSLog(const char*, ...);

// ============================================================================================
// Motion state for rigid bodies in the scene. Updates the map of changed 
// entities whenever the setWorldTransform callback is fired
class SimMotionState : public btMotionState
{
public:
	btRigidBody* RigidBody;

    SimMotionState(unsigned int id, const btTransform& startTransform, std::map<unsigned int, EntityProperties*>* updatesThisFrame)
		: m_properties(id, startTransform), m_lastProperties(id, startTransform)
	{
        m_xform = startTransform;
		m_updatesThisFrame = updatesThisFrame;
    }

    virtual ~SimMotionState()
	{
		m_updatesThisFrame->erase(m_properties.ID);
    }

    virtual void getWorldTransform(btTransform& worldTrans) const
	{
        worldTrans = m_xform;
    }

    virtual void setWorldTransform(const btTransform& worldTrans)
	{
		m_xform = worldTrans;
		Vector3 ZeroVect;

		// Put the new transform into m_properties
		m_properties.Position = m_xform.getOrigin();
		m_properties.Rotation = m_xform.getRotation();
		// The problem is that we don't get an event when an object is slept.
		// This means that the last non-zero values for linear and angular velocity
		// are left in the viewer who does dead reconning and the objects look like
		// they float off.
		// TODO: figure out how to generate a transform event when an object sleeps.
		m_properties.Velocity = RigidBody->getLinearVelocity();
		m_properties.AngularVelocity = RigidBody->getAngularVelocity();

		// Is this transform any different from the previous one?
		if (!m_properties.Position.AlmostEqual(m_lastProperties.Position, POSITION_TOLERANCE) ||
			!m_properties.Rotation.AlmostEqual(m_lastProperties.Rotation, ROTATION_TOLERANCE) ||
			// AlmostEqual doesn't catch very small changes when deactivating
			// !m_properties.Velocity.AlmostEqual(m_lastProperties.Velocity, VELOCITY_TOLERANCE) ||
			// !m_properties.AngularVelocity.AlmostEqual(m_lastProperties.AngularVelocity, ANGULARVELOCITY_TOLERANCE))
			// Maybe just push when the properties are being forced to deactivation
			// || (m_properties.Velocity == ZeroVect && m_properties.AngularVelocity == ZeroVect)
			// Check for exact changing in these because we don't want to miss deactivation
			!(m_properties.Velocity == m_lastProperties.Velocity) ||
			!(m_properties.AngularVelocity == m_lastProperties.AngularVelocity)
			)
		{
			// If so, update the previous transform and add this update to the list of 
			// updates this frame
			m_lastProperties = m_properties;
			(*m_updatesThisFrame)[m_properties.ID] = &m_properties;
		}
    }

protected:
	// forward reference: UpdatesThisFrameMapType* m_updatesThisFrame;
	std::map<unsigned int, EntityProperties*>* m_updatesThisFrame;
    btTransform m_xform;
	EntityProperties m_properties;
	EntityProperties m_lastProperties;
};

// ============================================================================================
// Callback for convex sweeps that excludes the object being swept
class ClosestNotMeConvexResultCallback : public btCollisionWorld::ClosestConvexResultCallback
{
public:
	ClosestNotMeConvexResultCallback (btCollisionObject* me) : btCollisionWorld::ClosestConvexResultCallback(btVector3(0.0, 0.0, 0.0), btVector3(0.0, 0.0, 0.0))
	{
		m_me = me;
	}

	virtual btScalar addSingleResult(btCollisionWorld::LocalConvexResult& convexResult,bool normalInWorldSpace)
	{
		// Ignore collisions with ourself and phantom objects
		if (convexResult.m_hitCollisionObject == m_me || IsPhantom(convexResult.m_hitCollisionObject))
			return 1.0;

		return ClosestConvexResultCallback::addSingleResult (convexResult, normalInWorldSpace);
	}
protected:
	btCollisionObject* m_me;
};

// ============================================================================================
// Callback for raycasts that excludes the object doing the raycast
class ClosestNotMeRayResultCallback : public btCollisionWorld::ClosestRayResultCallback
{
public:
	ClosestNotMeRayResultCallback (btCollisionObject* me) : btCollisionWorld::ClosestRayResultCallback(btVector3(0.0, 0.0, 0.0), btVector3(0.0, 0.0, 0.0))
	{
		m_me = me;
	}

	virtual btScalar addSingleResult(btCollisionWorld::LocalRayResult& rayResult,bool normalInWorldSpace)
	{
		if (rayResult.m_collisionObject == m_me || IsPhantom(rayResult.m_collisionObject))
			return 1.0;

		return ClosestRayResultCallback::addSingleResult (rayResult, normalInWorldSpace);
	}
protected:
	btCollisionObject* m_me;
};

// ============================================================================================
// Callback for non-moving overlap tests
class ContactSensorCallback : public btCollisionWorld::ContactResultCallback
{
public:
	btVector3 mOffset;

	ContactSensorCallback(btCollisionObject* collider)
		: btCollisionWorld::ContactResultCallback(), m_me(collider), m_maxPenetration(0.0), mOffset(0.0, 0.0, 0.0)
	{
	}

	virtual	btScalar addSingleResult(btManifoldPoint& cp, const btCollisionObject* colObj0, int partId0, int index0, const btCollisionObject* colObj1, int partId1, int index1)
	{
		// Ignore terrain collisions
		if (colObj0->getCollisionShape()->getShapeType() == TRIANGLE_SHAPE_PROXYTYPE ||
			colObj1->getCollisionShape()->getShapeType() == TRIANGLE_SHAPE_PROXYTYPE)
		{
			return 0;
		}

		// Ignore collisions with phantom objects
		if (IsPhantom(colObj0) || IsPhantom(colObj1))
		{
			return 0;
		}

		btScalar distance = cp.getDistance();

		if (distance < m_maxPenetration)
		{
			m_maxPenetration = distance;

			// Figure out if we are the first or second body in the collision 
			// pair so we know which direction to point the collision normal
			btScalar directionSign = (colObj0 == m_me) ? btScalar(-1.0) : btScalar(1.0);

			// Push offset back using the collision normal and the depth of the collision
			btVector3 touchingNormal = cp.m_normalWorldOnB * directionSign;
			mOffset = touchingNormal * distance;
		}

		return 0;
	}

protected:
	btCollisionObject* m_me;
	btScalar m_maxPenetration;
};

// ============================================================================================
// The main physics simulation class.
class BulletSim
{
	// Pointer to block of parameters passed from the managed code on initialization
	ParamBlock* m_params;

	// Simulation
	btDiscreteDynamicsWorld* m_dynamicsWorld;

	// Collision shapes
	btStaticPlaneShape* m_planeShape;
	btHeightfieldTerrainShape* m_heightfieldShape;

	// Mesh data and scene objects
	typedef std::map<unsigned long long, btBvhTriangleMeshShape*> MeshesMapType;
	MeshesMapType m_meshes;
	typedef std::map<unsigned long long, btCompoundShape*> HullsMapType;
	HullsMapType m_hulls;
	typedef std::map<unsigned int, btRigidBody*> BodiesMapType;
	BodiesMapType m_bodies;
	typedef std::map<unsigned int, btRigidBody*> CharactersMapType;
	CharactersMapType m_characters;
	typedef std::map<unsigned long long, btGeneric6DofConstraint*> ConstraintMapType;
	ConstraintMapType m_constraints;

	// Bullet world objects
	btBroadphaseInterface* m_broadphase;
	btCollisionDispatcher* m_dispatcher;
	btConstraintSolver*	m_solver;
	btDefaultCollisionConfiguration* m_collisionConfiguration;

	// Terrain and world metadata
	float* m_heightmapData;
	btVector3 m_minPosition;
	btVector3 m_maxPosition;

	// Used to expose updates from Bullet to the BulletSim API
	typedef std::map<unsigned int, EntityProperties*> UpdatesThisFrameMapType;
	UpdatesThisFrameMapType m_updatesThisFrame;
	int m_maxUpdatesPerFrame;
	EntityProperties* m_updatesThisFrameArray;

	// Used to expose colliders from Bullet to the BulletSim API
	int m_maxCollisionsPerFrame;
	CollisionDesc* m_collidersThisFrameArray;

public:

	BulletSim(btScalar maxX, btScalar maxY, btScalar maxZ);

	virtual ~BulletSim()
	{
		exitPhysics();
	}

	btDynamicsWorld* GetDynamicsWorld()
	{
		return m_dynamicsWorld;
	}

	void initPhysics(ParamBlock* parms, int maxCollisions, CollisionDesc* collisionArray, int maxUpdates, EntityProperties* updateArray);
	void exitPhysics();

	int PhysicsStep(btScalar timeStep, int maxSubSteps, btScalar fixedTimeStep, 
		int* updatedEntityCount, EntityProperties** updatedEntities, int* collidersCount, CollisionDesc** colliders);
	void SetHeightmap(float* heightmap);
	bool CreateHull(unsigned long long meshKey, int hullCount, float* hulls);
	bool CreateMesh(unsigned long long meshKey, int indicesCount, int* indices, int verticesCount, float* vertices);
	bool CreateObject(ShapeData* shapeData);
	void CreateLinkset(int objectCount, ShapeData* shapeDatas);
	void AddConstraint(unsigned int id1, unsigned int id2, 
				btVector3& frame1, btQuaternion& frame1rot, 
				btVector3& frame2, btQuaternion& frame2rot,
				btVector3& lowLinear, btVector3& hiLinear, btVector3& lowAngular, btVector3& hiAngular);
	bool RemoveConstraintByID(unsigned int id1);
	bool RemoveConstraint(unsigned int id1, unsigned int id2);
	btVector3 GetObjectPosition(unsigned int id);
	bool SetObjectTranslation(unsigned int id, btVector3& position, btQuaternion& rotation);
	bool SetObjectVelocity(unsigned int id, btVector3& velocity);
	bool SetObjectAngularVelocity(unsigned int id, btVector3& angularVelocity);
	bool SetObjectForce(unsigned int id, btVector3& force);
	bool SetObjectScaleMass(unsigned int id, btVector3& scale, float mass, bool isDynamic);
	bool SetObjectCollidable(unsigned int id, bool collidable);
	bool SetObjectDynamic(unsigned int id, bool isDynamic, float mass);
	bool SetObjectBuoyancy(unsigned int id, float buoyancy);
	bool SetObjectProperties(unsigned int id, bool isStatic, bool isSolid, bool genCollisions, float mass);
	bool HasObject(unsigned int id);
	bool DestroyObject(unsigned int id);
	bool DestroyHull(unsigned long long meshKey);
	bool DestroyMesh(unsigned long long id);
	SweepHit ConvexSweepTest(unsigned int id, btVector3& fromPos, btVector3& targetPos, btScalar extraMargin);
	RaycastHit RayTest(unsigned int id, btVector3& from, btVector3& to);
	const btVector3 RecoverFromPenetration(unsigned int id);

	void UpdateParameter(unsigned int localID, const char* parm, float value);
	void DumpPhysicsStats();

protected:
	void CreateGroundPlane();
	void CreateTerrain();
	void SetTerrainPhysicalParameters(btRigidBody* body);

	btCollisionShape* CreateShape(ShapeData* data);

	void SetAvatarPhysicalParameters(btRigidBody* body, btScalar friction, btScalar restitution, const btVector3& velocity);
	void SetObjectPhysicalParameters(btRigidBody* body, btScalar friction, btScalar restitution, const btVector3& velocity);
	void SetObjectDynamic(btRigidBody* body, bool isDynamic, float mass);
	void SetObjectCollidable(btRigidBody* body, bool collidable);
	void SetObjectProperties(btRigidBody* body, bool isStatic, bool isSolid, bool genCollisions, float mass);

	unsigned long long GenConstraintID(unsigned int id1, unsigned int id2);
	bool RecalculateAllConstraintsByID(unsigned int id1);

	void AdjustScaleForCollisionMargin(btCollisionShape* body, btVector3& scale);

	btCompoundShape* DuplicateCompoundShape(btCompoundShape* origionalCompoundShape);
	btCollisionShape* DuplicateMeshShape(btBvhTriangleMeshShape* origionalTriangleMeshShape);

	SweepHit GenericConvexSweepTest(btCollisionObject* collisionObject, btVector3& fromPos, btVector3& targetPos);
};

#endif //BULLET_SIM_H

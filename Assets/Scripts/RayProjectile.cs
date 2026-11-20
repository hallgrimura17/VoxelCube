using Assets.Resources;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;

public class RayProjectile : MonoBehaviour
{
    public float CurrentSpeed, BaseSpeed, Damage, Force, PowerMultiplier = 1f;
    public ParticleSystem ps;
    public int LifeSpan = 1500;
    public float Gravity, HitRadius;
    public LayerMask HitMask, ReflectMask;
    public string HitSoundClip = "BlasterHit";
    public UnityEvent OnSpawn;
    public UnityEvent<Vector3> OnHit;

    public void FixedUpdate()
    {
        if (CurrentSpeed > 0 && LifeSpan > 0)
        {
            Travel(Time.fixedDeltaTime);
        }
        else if(ps.isPlaying)
        {
            StopPs();
        }
    }

    protected void Travel(float deltaTime)
    {
        transform.position = Cast(deltaTime);
        LifeSpan--;
    }

    public virtual Vector3 Cast(float deltaTime)
    {
        CalculateGravity(deltaTime);

        //HomeInOn(deltaTime);

        var origin = transform.position;
        var direction = transform.forward;
        var distance = CurrentSpeed * deltaTime;

        if(Physics.Raycast(origin, direction, out var hit, distance, HitMask, QueryTriggerInteraction.Ignore))
        {
            Hit(hit.point, hit.normal, hit.collider, direction);
            if (CurrentSpeed <= 0)
            {
                return hit.point;
            }
            if (((1 << hit.collider.gameObject.layer) & ReflectMask.value) != 0)
            {
                CurrentSpeed -= BaseSpeed * 0.1f;
                Debug.DrawRay(origin, direction, Color.green, 5f);
                direction = Vector3.Reflect(direction, hit.normal);
                Debug.DrawRay(origin, hit.normal, Color.yellow, 5f);
                Debug.DrawRay(origin, direction, Color.red, 5f);
                transform.forward = direction;
                return hit.point + (distance - hit.distance) * direction;
            }
        }
        return origin + distance * direction;
    }

    public void CalculateGravity(float deltaTime)
    {
        if (Gravity > 0)
        {
            transform.rotation = Quaternion.LookRotation(transform.forward + Physics.gravity * deltaTime * Gravity / CurrentSpeed, transform.up);
            CurrentSpeed = math.clamp(CurrentSpeed + Vector3.Dot(Physics.gravity, transform.forward) * deltaTime * Gravity, 0.001f, 200);
        }
    }

    public Vector3 AttackDirection;
    public float HomingStrength = 1f;

    //public void HomeInOn(float deltaTime)
    //{

    //    var target = HomingTargets[0];

    //    var diff = target.GetImportantPosition - transform.position;
    //    var normalizedDiff = diff.normalized;
    //    var dot = math.max(0.5f, Vector3.Dot(normalizedDiff, transform.forward) + Mathf.Sin(diff.x));
    //    transform.forward += dot * normalizedDiff * HomingStrength * deltaTime;
    //    HomingStrength += Time.fixedDeltaTime * 5f;
    //}

    public void Hit(Vector3 hitPoint, Vector3 hitNormal, Collider hitCollider, Vector3 pushDirection)
    {
        CurrentSpeed = 0;
        VoxelModifier.MineChunksAtPoint(hitPoint, hitNormal, 2f, Damage * PowerMultiplier * GameController.instance.miningDamageMultiplier);
        OnHit?.Invoke(hitPoint);
        SoundManager.instance.PlaySoundClipAtLocation(hitPoint, HitSoundClip);
        ParticleManager.instance.EmitParticleEffect("BlasterImpact", hitPoint, Quaternion.LookRotation(hitNormal), Mathf.RoundToInt(Mathf.Log(Damage)));
    }

    public void StopPs()
    {
        ps.Stop(true);
    }

    public void Initialize(float powerMultiplier)
    {
        PowerMultiplier = powerMultiplier;
        CurrentSpeed = powerMultiplier * BaseSpeed;
        OnSpawn?.Invoke();
    }
    public float SpeedRatio => CurrentSpeed / BaseSpeed;

    void OnDrawGizmos()
    {
        var endposition = transform.position + CurrentSpeed * transform.forward;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, HitRadius);
        Gizmos.DrawLine(transform.position, endposition);
        Gizmos.DrawWireSphere(endposition, HitRadius);
    }
}

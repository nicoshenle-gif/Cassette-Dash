using UnityEngine;

/// <summary>
/// Falling cassette mechanical hazard projectile dropped by CassetteOverheadDropper. Deals 50 HP damage on hit.
/// </summary>
public class CassetteHazardProjectile : MonoBehaviour
{
    public CassettePlayer player;
    public float damage = 50f;
    public float gravity = 8.2f;
    public float floorY = -7.5f;

    float _vy = -2.2f;
    float _lifeTimer;
    Vector3 _spinSpeed = new Vector3(90f, 150f, 220f);

    void FixedUpdate()
    {
        if (!player) player = FindFirstObjectByType<CassettePlayer>();

        _lifeTimer += Time.fixedDeltaTime;
        if (_lifeTimer > 7f)
        {
            Destroy(gameObject);
            return;
        }

        _vy -= gravity * Time.fixedDeltaTime;
        transform.position += new Vector3(0f, _vy * Time.fixedDeltaTime, 0f);
        transform.Rotate(_spinSpeed * Time.fixedDeltaTime, Space.Self);

        if (player != null && !player.isDead)
        {
            Vector3 pPos = player.transform.position;
            float dx = Mathf.Abs(pPos.x - transform.position.x);
            float dz = Mathf.Abs(pPos.z - transform.position.z);
            float dy = Mathf.Abs(pPos.y - transform.position.y);

            if (dx < (player.CapsuleRadius + 0.34f) &&
                dz < (player.CapsuleRadius + 0.42f) &&
                dy < (player.CapsuleHalfHeight + 0.34f))
            {
                player.TakeDamage(damage);
                Destroy(gameObject);
                return;
            }
        }

        if (transform.position.y <= floorY)
        {
            Destroy(gameObject);
        }
    }
}

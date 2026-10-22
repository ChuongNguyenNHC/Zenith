using UnityEngine;

/// <summary>
/// Interface chung cho tất cả các đối tượng có thể nhận đòn đánh trong game (Dummy, Quái thường, Boss, v.v.)
/// </summary>
public interface IDamageable
{
    void TakeHit(Vector3 hitDirection, float knockbackForce);
}

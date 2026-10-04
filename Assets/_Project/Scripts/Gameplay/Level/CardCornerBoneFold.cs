using UnityEngine;

public class CardCornerBoneFold : MonoBehaviour
{
	[Header("Kéo cái GameObject xương góc trên bên trái vào đây")]
	public Transform topLeftBone;

	[Header("Độ kéo gập góc (X, Y, Z)")]
	public float pullX = 1.0f;
	public float pullY = -1.0f;
	public float pullZ = -0.5f; // Kéo lệch ra không gian 3D để lộ mặt sau

	void Update()
	{
		if (topLeftBone != null)
		{
			// Trực tiếp dịch chuyển vị trí cục bộ của xương góc trên bên trái
			// Xương này sẽ kéo theo toàn bộ vùng lưới xung quanh uốn cong theo nhờ Sprite Skin
			Vector3 targetPos = new Vector3(pullX, pullY, pullZ);
			topLeftBone.localPosition = targetPos;
		}
	}
}
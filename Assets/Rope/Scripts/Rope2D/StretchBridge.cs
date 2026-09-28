using UnityEngine;
using System.Collections;

public class StretchBridge : MonoBehaviour
{
    public Joint2D joint;
    public CapsuleCollider2D capCol;
    public RopeNode thisNode;
    public RopeNode thatNode;

    private void LateUpdate()
    {
        if (!thatNode || !thisNode || !joint)
        {
            Destroy(this);
            return;
        }
        var p1 = Vector3.left * thisNode.nodeLength / 2f;
        var p2 = thisNode.transform.InverseTransformPoint(thatNode.transform.TransformPoint(Vector3.right * thatNode.nodeLength / 2f));
        capCol.offset = (p2 + p1) / 2f;
        var size = capCol.size;
        size.x = Mathf.Abs((p2 - p1).x);
        capCol.size = size;
    }
}

using UnityEngine;
using System.Collections;

public class RopeNode : MonoBehaviour
{
    [Header("Components")]
    public Rigidbody2D rb;
    public Collider2D col;
    public SpriteRenderer rend;

    [Header("Props")]
    public float nodeLength = 1;


    public StretchBridge stretchBridge { get; set; }



    public void SetUseLineRenderer(bool b)
    {
        rend.enabled = !b;
    }


    public void CutByMouse(Vector3 worldPosition, bool isLastElement = false)
    {
        var rope = GetComponentInParent<Rope>();
        if (!rope)
        {
            Debug.LogError("No rope to manage the cut");
        }

        var localPosition = transform.InverseTransformPoint(worldPosition);
        bool cutBefore = localPosition.x < 0;

        rope.SplitRopeAt(this, cutBefore, isLastElement);
    }

    // public void CutByElement(Vector3 worldPosition, bool isLastElement = false)
    // {
    //     var rope = GetComponentInParent<RopeCharacter>();
    //     if (!rope)
    //     {
    //         Debug.LogError("No rope to manage the cut");
    //     }

    //     var localPosition = transform.InverseTransformPoint(worldPosition);
    //     bool cutBefore = localPosition.x < 0;

    //     rope.SplitRopeByElementAt(this, cutBefore, isLastElement);
    // }



    public RopeMaker.FixEndType GetFixedType()
    {
        var joints = GetComponents<AnchoredJoint2D>();
        foreach (var j in joints)
        {
            if (j.enabled && !j.connectedBody)
            {
                if (j is FixedJoint2D) return RopeMaker.FixEndType.Fixed;
                else if (j is HingeJoint2D) return RopeMaker.FixEndType.Pivotable;
            }
        }
        return RopeMaker.FixEndType.Free;
    }





    private void OnDrawGizmosSelected()
    {
        var mt = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(Vector3.left * nodeLength / 2f, Vector3.right * nodeLength / 2f);
        Gizmos.matrix = mt;
    }
}

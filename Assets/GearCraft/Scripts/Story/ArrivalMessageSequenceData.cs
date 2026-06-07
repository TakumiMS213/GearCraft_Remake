using System;
using UnityEngine;

[CreateAssetMenu(fileName = "ArrivalMessageSequenceData", menuName = "GearCraft/Story/Arrival Message Sequence")]
public class ArrivalMessageSequenceData : ScriptableObject
{
    [SerializeField] private ArrivalMessagePageData[] pages = Array.Empty<ArrivalMessagePageData>();

    public ArrivalMessagePageData GetPage(int index)
    {
        if (pages == null || index < 0 || index >= pages.Length)
        {
            return null;
        }

        return pages[index];
    }
}

[Serializable]
public class ArrivalMessagePageData
{
    [SerializeField] private Sprite portrait;
    [TextArea(2, 5)]
    [SerializeField] private string text;
    [SerializeField, Min(0.1f)] private float autoAdvanceSeconds = 4f;

    [Header("Camera")]
    [SerializeField] private bool moveCamera = true;
    [SerializeField] private Vector3 cameraPosition;
    [SerializeField, Min(0.1f)] private float cameraOrthographicSize = 5f;
    [SerializeField, Min(0f)] private float cameraMoveDuration = 1.2f;

    public Sprite Portrait => portrait;
    public string Text => text;
    public float AutoAdvanceSeconds => autoAdvanceSeconds;
    public bool MoveCamera => moveCamera;
    public Vector3 CameraPosition => cameraPosition;
    public float CameraOrthographicSize => cameraOrthographicSize;
    public float CameraMoveDuration => cameraMoveDuration;
}

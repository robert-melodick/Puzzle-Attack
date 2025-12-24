using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class ScrollTextureOffset : MonoBehaviour
{
    public Vector2 speed = new Vector2(0.05f, 0.0f);

    Renderer _r;
    MaterialPropertyBlock _mpb;
    Vector2 _offset;

    void Awake()
    {
        _r = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
    }

    void Update()
    {
        _offset += speed * Time.deltaTime;

        // keep values small to avoid floating drift over long sessions
        _offset.x = Mathf.Repeat(_offset.x, 1f);
        _offset.y = Mathf.Repeat(_offset.y, 1f);

        _r.GetPropertyBlock(_mpb);
        _mpb.SetVector("_MainTex_ST", new Vector4(1, 1, _offset.x, _offset.y)); 
        _r.SetPropertyBlock(_mpb);
    }
}
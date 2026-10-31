using System;
using System.Collections.Generic;
using MVCore.Common;
using MVCore.GMDL;
using OpenTK.Mathematics;

public enum GIZMO_MODE { TRANSLATE, ROTATE, SCALE }

public enum GIZMO_PART_TYPE
{
    T_X, T_Y, T_Z,
    R_X, R_Y, R_Z,
    S_X, S_Y, S_Z, S_UNIFORM,
    NONE
}

public sealed class GizmoPart
{
    public readonly GIZMO_PART_TYPE type;
    public readonly string meshName;
    public readonly Vector3 axis;

    public GizmoPart(GIZMO_PART_TYPE type, string meshName, Vector3 axis)
    {
        this.type = type;
        this.meshName = meshName;
        this.axis = axis;
    }
}

//Viewport transform tool state and screen-space interaction. Rendering remains in RenderManager so
//this class owns no OpenGL objects and can safely be updated from either viewport input backend.
public sealed class TransformGizmo
{
    private const float HandleLength = 0.25f;
    private const float DesiredPixels = 96.0f;
    private const float HitPixels = 11.0f;

    private readonly Dictionary<GIZMO_MODE, List<GizmoPart>> modeParts = new();
    private Vector2 dragStartMouse;
    private Vector2 dragOriginScreen;
    private Vector2 dragAxisScreen;
    private Vector2 dragRotationU;
    private Vector2 dragRotationV;
    private Vector3 dragStartPosition;
    private Vector3 dragStartScale;
    private Matrix4 dragStartRotation;
    private Matrix4 dragStartWorld;
    private Matrix4 dragParentWorld;
    private float dragPixelsPerUnit;
    private float dragStartAngle;

    public Model reference { get; private set; }
    public GIZMO_MODE Mode { get; private set; } = GIZMO_MODE.TRANSLATE;
    public GIZMO_PART_TYPE HoveredPart { get; private set; } = GIZMO_PART_TYPE.NONE;
    public GIZMO_PART_TYPE ActivePart { get; private set; } = GIZMO_PART_TYPE.NONE;
    public bool IsDragging => ActivePart != GIZMO_PART_TYPE.NONE;
    public IReadOnlyList<GizmoPart> Parts => modeParts[Mode];

    public TransformGizmo()
    {
        modeParts[GIZMO_MODE.TRANSLATE] = new List<GizmoPart>
        {
            new(GIZMO_PART_TYPE.T_X, "default_translation_gizmo_x_axis", Vector3.UnitX),
            new(GIZMO_PART_TYPE.T_Y, "default_translation_gizmo_y_axis", Vector3.UnitY),
            new(GIZMO_PART_TYPE.T_Z, "default_translation_gizmo_z_axis", Vector3.UnitZ)
        };
        modeParts[GIZMO_MODE.ROTATE] = new List<GizmoPart>
        {
            new(GIZMO_PART_TYPE.R_X, "default_rotation_gizmo_x_axis", Vector3.UnitX),
            new(GIZMO_PART_TYPE.R_Y, "default_rotation_gizmo_y_axis", Vector3.UnitY),
            new(GIZMO_PART_TYPE.R_Z, "default_rotation_gizmo_z_axis", Vector3.UnitZ)
        };
        modeParts[GIZMO_MODE.SCALE] = new List<GizmoPart>
        {
            new(GIZMO_PART_TYPE.S_X, "default_scale_gizmo_x_axis", Vector3.UnitX),
            new(GIZMO_PART_TYPE.S_Y, "default_scale_gizmo_y_axis", Vector3.UnitY),
            new(GIZMO_PART_TYPE.S_Z, "default_scale_gizmo_z_axis", Vector3.UnitZ),
            new(GIZMO_PART_TYPE.S_UNIFORM, "default_scale_gizmo_uniform", Vector3.One)
        };
    }

    public void SetMode(GIZMO_MODE mode)
    {
        if (IsDragging)
            return;
        Mode = mode;
        HoveredPart = GIZMO_PART_TYPE.NONE;
    }

    public void SetReference(Model model)
    {
        reference = model;
        HoveredPart = GIZMO_PART_TYPE.NONE;
        ActivePart = GIZMO_PART_TYPE.NONE;
    }

    public void ClearReference() => SetReference(null);

    public Matrix4 GetWorldMatrix(Camera camera, int viewportHeight)
    {
        if (reference == null)
            return Matrix4.Identity;
        float scale = GetWorldScale(camera, viewportHeight);
        Matrix4 orientation = Matrix4.CreateFromQuaternion(reference.worldMat.ExtractRotation());
        return Matrix4.CreateScale(scale) * orientation * Matrix4.CreateTranslation(reference.worldPosition);
    }

    public bool UpdateHover(Vector2 mouse, int width, int height, Camera camera)
    {
        if (reference == null || IsDragging || width <= 0 || height <= 0 || camera == null)
            return false;
        GIZMO_PART_TYPE old = HoveredPart;
        HoveredPart = HitTest(mouse, width, height, camera);
        return old != HoveredPart;
    }

    public bool BeginDrag(Vector2 mouse, int width, int height, Camera camera)
    {
        if (reference == null || HoveredPart == GIZMO_PART_TYPE.NONE || camera == null)
            return false;

        ActivePart = HoveredPart;
        dragStartMouse = mouse;
        dragStartPosition = reference.__localPosition;
        dragStartScale = reference.__localScale;
        dragStartRotation = reference.__localRotation;
        dragStartWorld = reference.worldMat;
        dragParentWorld = reference.parent?.worldMat ?? Matrix4.Identity;

        Project(reference.worldPosition, width, height, camera, out dragOriginScreen);
        Vector3 axis = WorldAxis(PartAxis(ActivePart), dragStartWorld);
        float worldScale = GetWorldScale(camera, height);
        Project(reference.worldPosition + axis * worldScale * HandleLength, width, height, camera,
                out Vector2 endpoint);
        dragAxisScreen = endpoint - dragOriginScreen;
        dragPixelsPerUnit = Math.Max(dragAxisScreen.Length / Math.Max(worldScale * HandleLength, 1e-6f), 1e-3f);

        if (Mode == GIZMO_MODE.ROTATE)
        {
            BuildPlaneBasis(axis, out Vector3 u, out Vector3 v);
            Project(reference.worldPosition + u * worldScale * HandleLength, width, height, camera, out Vector2 pu);
            Project(reference.worldPosition + v * worldScale * HandleLength, width, height, camera, out Vector2 pv);
            dragRotationU = SafeNormalized(pu - dragOriginScreen, Vector2.UnitX);
            dragRotationV = SafeNormalized(pv - dragOriginScreen, Vector2.UnitY);
            dragStartAngle = MouseAngle(mouse);
        }
        return true;
    }

    public bool Drag(Vector2 mouse)
    {
        if (reference == null || !IsDragging)
            return false;

        Vector3 position = dragStartPosition;
        Vector3 scale = dragStartScale;
        Matrix4 rotation = dragStartRotation;
        Vector3 localAxis = PartAxis(ActivePart);

        if (Mode == GIZMO_MODE.TRANSLATE)
        {
            Vector2 screenAxis = SafeNormalized(dragAxisScreen, Vector2.UnitX);
            float worldDistance = Vector2.Dot(mouse - dragStartMouse, screenAxis) / dragPixelsPerUnit;
            Vector3 worldPosition = dragStartWorld.ExtractTranslation() + WorldAxis(localAxis, dragStartWorld) * worldDistance;
            position = (new Vector4(worldPosition, 1.0f) * dragParentWorld.Inverted()).Xyz;
        }
        else if (Mode == GIZMO_MODE.SCALE)
        {
            float delta;
            if (ActivePart == GIZMO_PART_TYPE.S_UNIFORM)
                delta = -(mouse.Y - dragStartMouse.Y) / Math.Max(dragAxisScreen.Length, 32.0f);
            else
                delta = Vector2.Dot(mouse - dragStartMouse, SafeNormalized(dragAxisScreen, Vector2.UnitX)) /
                        Math.Max(dragAxisScreen.Length, 32.0f);
            float factor = Math.Max(0.01f, 1.0f + delta);
            if (ActivePart == GIZMO_PART_TYPE.S_UNIFORM)
                scale = dragStartScale * factor;
            else if (localAxis.X > 0.5f)
                scale.X = Math.Max(0.001f, dragStartScale.X * factor);
            else if (localAxis.Y > 0.5f)
                scale.Y = Math.Max(0.001f, dragStartScale.Y * factor);
            else
                scale.Z = Math.Max(0.001f, dragStartScale.Z * factor);
        }
        else
        {
            float angle = MouseAngle(mouse) - dragStartAngle;
            rotation = Matrix4.CreateFromAxisAngle(localAxis, angle) * dragStartRotation;
        }

        //Use the normal invalidation path while dragging. It is more expensive than a stable-record-only
        //update, but it also refreshes culling and every render representation of container nodes.
        reference.SetUserTransformFromMatrix(position, rotation, scale, true);
        return true;
    }

    public void EndDrag()
    {
        ActivePart = GIZMO_PART_TYPE.NONE;
        HoveredPart = GIZMO_PART_TYPE.NONE;
    }

    private GIZMO_PART_TYPE HitTest(Vector2 mouse, int width, int height, Camera camera)
    {
        if (!Project(reference.worldPosition, width, height, camera, out Vector2 origin))
            return GIZMO_PART_TYPE.NONE;

        float worldScale = GetWorldScale(camera, height);
        float best = HitPixels;
        GIZMO_PART_TYPE result = GIZMO_PART_TYPE.NONE;
        foreach (GizmoPart part in Parts)
        {
            if (part.type == GIZMO_PART_TYPE.S_UNIFORM)
            {
                float d = (mouse - origin).Length;
                if (d < best) { best = d; result = part.type; }
                continue;
            }

            Vector3 axis = WorldAxis(part.axis, reference.worldMat);
            if (Mode == GIZMO_MODE.ROTATE)
            {
                BuildPlaneBasis(axis, out Vector3 u, out Vector3 v);
                Vector2 previous = default;
                bool havePrevious = false;
                const int segments = 64;
                for (int i = 0; i <= segments; i++)
                {
                    float a = MathHelper.TwoPi * i / segments;
                    Vector3 point = reference.worldPosition +
                        (u * MathF.Cos(a) + v * MathF.Sin(a)) * worldScale * HandleLength;
                    if (!Project(point, width, height, camera, out Vector2 current))
                        continue;
                    if (havePrevious)
                    {
                        float d = DistanceToSegment(mouse, previous, current);
                        if (d < best) { best = d; result = part.type; }
                    }
                    previous = current;
                    havePrevious = true;
                }
            }
            else if (Project(reference.worldPosition + axis * worldScale * HandleLength,
                             width, height, camera, out Vector2 endpoint))
            {
                float d = DistanceToSegment(mouse, origin, endpoint);
                if (d < best) { best = d; result = part.type; }
            }
        }
        return result;
    }

    private float MouseAngle(Vector2 mouse)
    {
        Vector2 d = mouse - dragOriginScreen;
        return MathF.Atan2(Vector2.Dot(d, dragRotationV), Vector2.Dot(d, dragRotationU));
    }

    private float GetWorldScale(Camera camera, int viewportHeight)
    {
        if (viewportHeight <= 0)
            return 1.0f;
        Vector3 displayedOrigin = (new Vector4(reference.worldPosition, 1.0f) * RenderState.rotMat).Xyz;
        if (camera.type != 0)
            return DesiredPixels / viewportHeight * 2.0f / HandleLength;
        float distance = Math.Max((displayedOrigin - camera.Position).Length, camera.zNear * 2.0f);
        float visibleHeight = 2.0f * distance * MathF.Tan(0.5f * camera.settings._fovRadians);
        return visibleHeight * DesiredPixels / viewportHeight / HandleLength;
    }

    private static bool Project(Vector3 point, int width, int height, Camera camera, out Vector2 screen)
    {
        Vector4 clip = new Vector4(point, 1.0f) * RenderState.rotMat * camera.viewMat;
        if (Math.Abs(clip.W) < 1e-6f || clip.W <= 0.0f)
        {
            screen = default;
            return false;
        }
        Vector3 ndc = clip.Xyz / clip.W;
        screen = new Vector2((ndc.X * 0.5f + 0.5f) * width,
                             (0.5f - ndc.Y * 0.5f) * height);
        return true;
    }

    private static Vector3 PartAxis(GIZMO_PART_TYPE type)
    {
        return type is GIZMO_PART_TYPE.T_X or GIZMO_PART_TYPE.R_X or GIZMO_PART_TYPE.S_X ? Vector3.UnitX :
               type is GIZMO_PART_TYPE.T_Y or GIZMO_PART_TYPE.R_Y or GIZMO_PART_TYPE.S_Y ? Vector3.UnitY :
               type is GIZMO_PART_TYPE.T_Z or GIZMO_PART_TYPE.R_Z or GIZMO_PART_TYPE.S_Z ? Vector3.UnitZ :
               Vector3.One;
    }

    private static Vector3 WorldAxis(Vector3 localAxis, Matrix4 world)
    {
        Matrix4 orientation = Matrix4.CreateFromQuaternion(world.ExtractRotation());
        return (new Vector4(localAxis, 0.0f) * orientation).Xyz.Normalized();
    }

    private static void BuildPlaneBasis(Vector3 normal, out Vector3 u, out Vector3 v)
    {
        Vector3 helper = Math.Abs(normal.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX;
        u = Vector3.Cross(normal, helper).Normalized();
        v = Vector3.Cross(normal, u).Normalized();
    }

    private static Vector2 SafeNormalized(Vector2 value, Vector2 fallback)
    {
        return value.LengthSquared < 1e-6f ? fallback : value.Normalized();
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float denom = ab.LengthSquared;
        if (denom < 1e-6f)
            return (p - a).Length;
        float t = Math.Clamp(Vector2.Dot(p - a, ab) / denom, 0.0f, 1.0f);
        return (p - (a + t * ab)).Length;
    }
}

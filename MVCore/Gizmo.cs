using System;
using System.Collections.Generic;
using MVCore;
using MVCore.GMDL;
using MVCore.GMDL.Primitives;
using OpenTK;

public enum GIZMO_PART_TYPE{
	T_X = 0x0,
	T_Y,
	T_Z,
	R_X,
	R_Y,
	R_Z,
	S_X,
	S_Y,
	S_Z,
	NONE
}

public class GizmoPart
{
	public GIZMO_PART_TYPE type;
	public Vector3 pick_color;
	public bool active = false;
	public GLMeshVao meshVao;
	public bool isSelected = false;
	
	
	public GizmoPart(GIZMO_PART_TYPE t, Vector3 col)
    {
		type = t;
		pick_color = col;
		switch (t)
        {
			case GIZMO_PART_TYPE.T_X:
				meshVao = MVCore.Common.RenderState.activeResMgr.GLPrimitiveMeshVaos["default_translation_gizmo_x_axis"];
				break;
			case GIZMO_PART_TYPE.T_Y:
				meshVao = MVCore.Common.RenderState.activeResMgr.GLPrimitiveMeshVaos["default_translation_gizmo_y_axis"];
				break;
			case GIZMO_PART_TYPE.T_Z:
				meshVao = MVCore.Common.RenderState.activeResMgr.GLPrimitiveMeshVaos["default_translation_gizmo_z_axis"];
				break;
		}
		
    }
	
	
	public void update()
    {

    }


}

public abstract class Gizmo
{
	
	public List<GizmoPart> gizmoParts;
	public Model reference;

	public bool isActive
    {
		get
        {
			foreach (GizmoPart gz in gizmoParts)
				if (gz.active) return true;
            return false;
        }
    }

	public GIZMO_PART_TYPE activeType
	{
		get
		{
			foreach (GizmoPart gz in gizmoParts)
				if (gz.active) return gz.type;
			return GIZMO_PART_TYPE.NONE;
		}
	}


	public Gizmo()
    {
		gizmoParts = new List<GizmoPart>();
    }

	public void updateMeshInfo()
	{
		if (reference != null)
		{
			foreach (GizmoPart g in gizmoParts)
            {
				GLMeshBufferManager.addInstance(ref g.meshVao, reference);
				GLMeshBufferManager.setInstanceSelectedStatus(g.meshVao, g.meshVao.instance_count - 1, g.active);
			}
				
		}

	}

	public void reset()
    {
		foreach (GizmoPart g in gizmoParts)
        {
			g.active = false;
		}
			
	
	}
}


public class TranslationGizmo : Gizmo
{
	
	public TranslationGizmo()
	{
		gizmoParts.Add(new GizmoPart(GIZMO_PART_TYPE.T_X, new Vector3(1.0f, 0.0f, 0.0f)));
		gizmoParts.Add(new GizmoPart(GIZMO_PART_TYPE.T_Y, new Vector3(0.0f, 1.0f, 0.0f)));
		gizmoParts.Add(new GizmoPart(GIZMO_PART_TYPE.T_Z, new Vector3(0.0f, 0.0f, 1.0f)));
	}

	public void update()
    {
		updateTransform();
		//Update included axis parts
		foreach (GizmoPart g in gizmoParts)
			g.update();
	}

	public void setReference(Model m)
    {
		reference = m;
		updateTransform();
    }

	public void updateTransform()
    {
		//TODO: Do I need this?
    }


}

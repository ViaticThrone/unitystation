using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShroudTile : MonoBehaviour {

	private bool shouldCheck = false;
	public Renderer renderer;
	void OnEnable(){
		renderer.enabled = true;
		shouldCheck = true;
	}

    public void SetShroudStatus(bool enabled){
        renderer.enabled = enabled;
    }

//	void Update(){
//		if (shouldCheck) {
//			if (!renderer.isVisible) {
//				shouldCheck = false;
//				ReturnToPool();
//			}
//		}
//	}
//		
//	private void ReturnToPool(){
//		DisplayManager.Instance.fieldOfView.shroudTiles.Remove(transform.position);
//		PoolManager.PoolClientDestroy(gameObject);
//	}
}

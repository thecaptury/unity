/*using UnityEngine;
using System.Collections.Generic;


//====================
// integrates leap motion based hand tracking with The Captury's full body tracking
//====================
public class Controller : MonoBehaviour
{
	public CapturyNetworkPlugin networkPlugin;
	public CapturyLeapIntegration capturyLeapIntegration;
	
	public GameObject actorTemplateObject; // this object is cloned for each actor

	private List<CapturySkeleton> instantiateThese = new List<CapturySkeleton>();
	private List<CapturySkeleton> deleteThese = new List<CapturySkeleton>();
	
	private bool playerSet = false;

	void FoundSkeleton(CapturySkeleton skeleton)
	{
		Debug.Log("controller found skeleton " + skeleton.name + " id " + skeleton.id);
		lock (instantiateThese)
		{
			instantiateThese.Add(skeleton);
		}
	}
	
	void LostSkeleton(CapturySkeleton skeleton)
	{
		Debug.Log("controller lost skeleton " + skeleton.name + " id " + skeleton.id);
		lock (deleteThese)
		{
			deleteThese.Add(skeleton);
		}
	}

	//=============================
	// this is run once at startup
	//=============================
	void Start()
	{
		if (networkPlugin != null) {
			networkPlugin.foundSkeleton = FoundSkeleton;
			networkPlugin.lostSkeleton = LostSkeleton;
		}
	}

	//==========================
	// this is run once at exit
	//==========================
	void OnDisable()
	{
	}


	//============================
	// this is run once per frame
	//============================
	void Update()
	{
		lock (instantiateThese)
		{
			foreach (CapturySkeleton skel in instantiateThese) {
				Debug.Log("instantiating skeleton " + skel.name + " id " + skel.id);
				GameObject actor = (GameObject) Instantiate(actorTemplateObject);
				actor.SetActive(true);
				skel.mesh = actor;
				if (!playerSet) {
					Transform left = null, right = null;
					Component[] trafos = actor.transform.GetComponentsInChildren<Transform>();
					foreach (Transform child in trafos) {
						if (child.name.EndsWith("LeftFingerBase"))
							left = child;
						if (child.name.EndsWith("RightFingerBase"))
							right = child;
					}
					if (left != null && right != null) {
						capturyLeapIntegration.setTargetModel(left, right, skel.id);
					} else
						Debug.Log("cannot find hands on target actor");
				}
			}
			instantiateThese.Clear();
		}
		lock (deleteThese)
		{
			foreach (CapturySkeleton skel in instantiateThese) {
				Debug.Log("deleting skeleton " + skel.name + " id " + skel.id);
				Destroy(skel.mesh);
				skel.mesh = null;
			}
			deleteThese.Clear();
		}
	}
}
*/
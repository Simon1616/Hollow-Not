using UnityEngine;
using System.Collections;

[System.Serializable]
public class ColorToPrefab {
	public Color32 color;
	public GameObject prefab;
}

public class LevelLoader : MonoBehaviour {

	public string levelFileName;

	//public Texture2D levelMap;

	public ColorToPrefab[] colorToPrefab;


	// Use this for initialization
	void Awake () {
		LoadMap();
	}

	void EmptyMap() {
		// Find all of our children and...eliminate them.

		while(transform.childCount > 0) {
			Transform c = transform.GetChild(0);
			c.SetParent(null); // become Batman
			Destroy(c.gameObject); // become The Joker
		}
	}

	void LoadAllLevelNames() {
		// Read the list of files from StreamingAssets/Levels/*.png
		// The player will progess through the levels alphabetically
	}

	void LoadMap() {
		EmptyMap();

		// Read the image data from the file in StreamingAssets
		string filePath = Application.dataPath + "/StreamingAssets/" + levelFileName;
		byte[] bytes = System.IO.File.ReadAllBytes(filePath);
		Texture2D levelMap = new Texture2D(2, 2);
		levelMap.LoadImage(bytes);


		// Get the raw pixels from the level imagemap
		Color32[] allPixels = levelMap.GetPixels32();
		int width = levelMap.width;
		int height = levelMap.height;

		for (int x = 0; x < width; x++) {
			for (int y = 0; y < height; y++) {

				SpawnTileAt( allPixels[(y * width) + x], x, y );

			}
		}
	}

	void SpawnTileAt( Color32 c, int x, int y ) {

		// If this is a transparent pixel, then it's meant to just be empty.
		if(c.a <= 0) {
			return;
		}

		// Find the right color in our map

		// NOTE: This isn't optimized. You should have a dictionary lookup for max speed
		foreach(ColorToPrefab ctp in colorToPrefab) {
			
			if( c.Equals(ctp.color) ) {
				// Spawn the prefab at the right location
				GameObject go = (GameObject)Instantiate(ctp.prefab, new Vector3(x, y, 0), Quaternion.identity );
				go.transform.SetParent(this.transform);
				// maybe do more stuff to the gameobject here?
				return;
			}
		}

		// If we got to this point, it means we did not find a matching color in our array.

		Debug.LogError("No color to prefab found for: " + c.ToString() );

	}
	
}

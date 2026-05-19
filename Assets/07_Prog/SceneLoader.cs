using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[ExecuteInEditMode]
public class SceneLoader : MonoBehaviour
{
    [SerializeField] private Object m_sceneToLoad;
    private bool m_hasBeenLoaded = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (m_hasBeenLoaded) return;

        if (m_sceneToLoad)
        {
            if (!Application.isPlaying)
            {
                string path = AssetDatabase.GetAssetPath(m_sceneToLoad);
                //Debug.Log(path);
                EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }
            else
            {
                SceneManager.LoadScene(m_sceneToLoad.name, LoadSceneMode.Additive);

            }

            m_hasBeenLoaded = true;
        }
    }
}

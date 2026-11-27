using UnityEngine;

namespace Zvezda57.Documents
{
    public sealed class DocumentFolderVisual : MonoBehaviour
    {
        [SerializeField]
        private GameObject closedFolder;
        [SerializeField]
        private GameObject openedFolder;

        public void Show(bool opened)
        {
            closedFolder.SetActive(!opened);
            openedFolder.SetActive(opened);
        }
    }
}

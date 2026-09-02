using UnityEngine;
using UnityEngine.InputSystem;

public class ChildrenText : MonoBehaviour
{
    [SerializeField] private GameObject objCanvas = null; // �Ӹ� �� "P ��ư ��������" �ȳ��� UI
    [SerializeField] private MiniGameController miniGameController = null;

    private bool bPlayerInArea = false;

    void Start()
    {
        if (objCanvas != null) objCanvas.SetActive(false);
    }

    void Update()
    {
        if (objCanvas != null && objCanvas.activeSelf && Camera.main != null)
        {
            objCanvas.transform.rotation = Camera.main.transform.rotation;
        }

        // �÷��̾ ���� �ȿ� �ְ� �̴ϰ����� ���� ���� �ƴ� �� P �Է� ����
        if (bPlayerInArea && miniGameController != null && !miniGameController.IsGameActive)
        {
            if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
            {
                if (objCanvas != null) objCanvas.SetActive(false); // �ȳ� �ؽ�Ʈ �����
                miniGameController.StartMiniGame();               // �̴ϰ��� ����
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            bPlayerInArea = true;
            // �̴ϰ��� ���� ���� �ƴ� ���� �Ӹ� �� �ȳ��� Ȱ��ȭ
            if (miniGameController != null && !miniGameController.IsGameActive)
            {
                if (objCanvas != null) objCanvas.SetActive(true);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            bPlayerInArea = false;
            if (objCanvas != null) objCanvas.SetActive(false);

            // �̴ϰ��� ���� ���� �÷��̾ ������ ����ġ�� ���� ó��
            if (miniGameController != null && miniGameController.IsGameActive)
            {
                miniGameController.EndMiniGame(false);
            }
        }
    }

    // �̴ϰ��ӿ��� ���� �������� �� �ȳ� �ؽ�Ʈ�� �����ϰ� �ٽ� ���ִ� ��� �Լ�
    public void ResetTextUI()
    {
        if (bPlayerInArea && objCanvas != null && miniGameController != null && !miniGameController.IsGameActive)
        {
            objCanvas.SetActive(true);
        }
    }
}
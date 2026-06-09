using UnityEngine;
using UnityEngine.EventSystems;
using TowerDefense.Data;

namespace TowerDefense.Hero
{
    public class HeroController : MonoBehaviour
    {
        [Header("Hero")]
        [SerializeField] private Hero _hero;
        [SerializeField] private HeroData _heroData;

        [Header("Movement Settings")]
        [SerializeField] private bool _useClickMovement = true;
        [SerializeField] private LayerMask _groundLayer;

        [Header("Skill Keys")]
        [SerializeField] private KeyCode _skill1Key = KeyCode.Q;
        [SerializeField] private KeyCode _skill2Key = KeyCode.W;
        [SerializeField] private KeyCode _skill3Key = KeyCode.E;

        private Camera _mainCamera;

        private void Awake()
        {
            _mainCamera = Camera.main;

            if (_hero == null)
            {
                _hero = GetComponent<Hero>();
            }
        }

        private void Start()
        {
            if (_hero != null && _heroData != null)
            {
                _hero.Initialize(_heroData);
            }
        }

        private void Update()
        {
            if (_hero == null || _hero.IsDead) return;

            HandleMovement();
            HandleSkills();
        }

        private void HandleMovement()
        {
            if (!_useClickMovement) return;

            if (Input.GetMouseButtonDown(1) && !EventSystem.current.IsPointerOverGameObject())
            {
                Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction, Mathf.Infinity, _groundLayer);

                if (hit.collider != null)
                {
                    _hero.MoveTo(hit.point);
                }
                else
                {
                    Vector3 worldPosition = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
                    worldPosition.z = 0;
                    _hero.MoveTo(worldPosition);
                }
            }
        }

        private void HandleSkills()
        {
            if (_heroData == null || _heroData.skills == null) return;

            if (Input.GetKeyDown(_skill1Key) && _heroData.skills.Length > 0)
            {
                _hero.UseSkill(0);
            }
            else if (Input.GetKeyDown(_skill2Key) && _heroData.skills.Length > 1)
            {
                _hero.UseSkill(1);
            }
            else if (Input.GetKeyDown(_skill3Key) && _heroData.skills.Length > 2)
            {
                _hero.UseSkill(2);
            }
        }

        public void UseSkillByIndex(int index)
        {
            if (_hero != null)
            {
                _hero.UseSkill(index);
            }
        }

        public void SetHeroData(HeroData data)
        {
            _heroData = data;
            if (_hero != null)
            {
                _hero.Initialize(_heroData);
            }
        }
    }
}

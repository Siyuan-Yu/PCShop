using PCShopSell.Core.Session;
using UnityEngine;
using UnityEngine.UI;

namespace PCShopSell.Game.UI
{
    internal sealed class CompanyDepartmentGlyph : MaskableGraphic
    {
        private CompanyDepartment _department;
        private Rect _square;

        internal void Configure(CompanyDepartment department, Color tint)
        {
            _department = department;
            color = tint;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = rectTransform.rect;
            float size = Mathf.Min(bounds.width, bounds.height) * .92f;
            _square = new Rect(bounds.center - Vector2.one * size / 2, Vector2.one * size);
            switch (_department)
            {
                case CompanyDepartment.Personnel:
                    Box(mesh, .36f, .55f, .64f, .86f);
                    Stroke(mesh, .19f, .19f, .19f, .39f);
                    Stroke(mesh, .19f, .39f, .31f, .47f);
                    Stroke(mesh, .31f, .47f, .69f, .47f);
                    Stroke(mesh, .69f, .47f, .81f, .39f);
                    Stroke(mesh, .81f, .39f, .81f, .19f);
                    Stroke(mesh, .81f, .19f, .19f, .19f);
                    break;
                case CompanyDepartment.Research:
                    Stroke(mesh, .37f, .85f, .63f, .85f);
                    Stroke(mesh, .42f, .85f, .42f, .61f);
                    Stroke(mesh, .58f, .85f, .58f, .61f);
                    Stroke(mesh, .42f, .61f, .18f, .23f);
                    Stroke(mesh, .58f, .61f, .82f, .23f);
                    Stroke(mesh, .18f, .23f, .82f, .23f);
                    Stroke(mesh, .31f, .43f, .69f, .43f);
                    break;
                case CompanyDepartment.Finance:
                    Box(mesh, .25f, .13f, .75f, .87f);
                    Box(mesh, .33f, .58f, .67f, .77f);
                    for (int row = 0; row < 2; row++)
                    for (int col = 0; col < 2; col++)
                    {
                        float x = .35f + col * .22f, y = .26f + row * .17f;
                        Stroke(mesh, x, y, x + .08f, y);
                    }
                    break;
                case CompanyDepartment.Operations:
                    Box(mesh, .23f, .15f, .77f, .64f);
                    Box(mesh, .42f, .15f, .61f, .42f);
                    Stroke(mesh, .13f, .64f, .25f, .84f);
                    Stroke(mesh, .25f, .84f, .75f, .84f);
                    Stroke(mesh, .75f, .84f, .87f, .64f);
                    Stroke(mesh, .87f, .64f, .13f, .64f);
                    break;
                case CompanyDepartment.Marketing:
                    Box(mesh, .14f, .42f, .43f, .63f);
                    Stroke(mesh, .43f, .42f, .73f, .27f);
                    Stroke(mesh, .43f, .63f, .73f, .78f);
                    Stroke(mesh, .73f, .27f, .73f, .78f);
                    Stroke(mesh, .26f, .42f, .35f, .18f);
                    Stroke(mesh, .84f, .53f, .96f, .53f);
                    break;
            }
        }

        private void Box(VertexHelper mesh, float left, float bottom, float right, float top)
        {
            Stroke(mesh, left, bottom, right, bottom);
            Stroke(mesh, right, bottom, right, top);
            Stroke(mesh, right, top, left, top);
            Stroke(mesh, left, top, left, bottom);
        }

        private void Stroke(VertexHelper mesh, float x1, float y1, float x2, float y2)
        {
            Vector2 start = new Vector2(_square.x + x1 * _square.width, _square.y + y1 * _square.height);
            Vector2 end = new Vector2(_square.x + x2 * _square.width, _square.y + y2 * _square.height);
            Vector2 direction = end - start;
            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * _square.width * .034f;
            int offset = mesh.currentVertCount;
            mesh.AddVert(start + normal, color, Vector2.zero);
            mesh.AddVert(start - normal, color, Vector2.zero);
            mesh.AddVert(end - normal, color, Vector2.zero);
            mesh.AddVert(end + normal, color, Vector2.zero);
            mesh.AddTriangle(offset, offset + 1, offset + 2);
            mesh.AddTriangle(offset, offset + 2, offset + 3);
        }
    }
}

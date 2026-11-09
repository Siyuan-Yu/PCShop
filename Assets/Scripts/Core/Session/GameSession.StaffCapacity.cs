using System;
using System.Collections.Generic;
using System.Linq;

namespace PCShopSell.Core.Session
{
    public sealed class AdditionalShopEmployee { public EmployeeRole Role; public Employee Person; }
    [Serializable] public sealed class SavedAdditionalEmployee { public string ShopId; public int Role; public SaveEmployee Person; }
    [Serializable] public sealed class SavedHireShop { public string EmployeeId,ShopId; }
    public sealed partial class SaveData
    {
        public SavedAdditionalEmployee[] additionalShopEmployees;
        public SavedHireShop[] employeeHireShops;
    }
    public sealed partial class GameSession
    {
        private readonly Dictionary<string,string> _employeeHireShops=new Dictionary<string,string>();
        public IEnumerable<Employee> ShopStaff(OwnedShop shop,EmployeeRole role)
        {
            if(shop==null)yield break;
            var primary=EmployeeAt(shop,role);if(primary!=null)yield return primary;
            foreach(var extra in shop.AdditionalEmployees.Where(e=>e.Role==role))yield return extra.Person;
        }
        public int StaffCommitted(OwnedShop shop,string exceptId=null)
        {
            return EmployeesForShop(shop).Where(e=>e.Person.Id!=exceptId)
                .Select(e=>e.Person.Id).Distinct().Count();
        }
        public EmployeeAssignment[] EmployeesForShop(OwnedShop shop)=>shop==null?Array.Empty<EmployeeAssignment>():
            Employees.Where(e=>e.Shop==shop||e.Person.TransferShopId==shop.Place.Id
                ||e.Shop==null&&_employeeHireShops.TryGetValue(e.Person.Id,out var hired)&&hired==shop.Place.Id).ToArray();
        public string StaffCapacityBlocker(OwnedShop shop,string exceptId=null,bool replace=false,EmployeeRole role=EmployeeRole.Unassigned)
        {
            if(shop==null)return "请选择门店";
            int count=StaffCommitted(shop,exceptId);
            return count>=StaffLimit(shop.Place.Size)?SizeName(shop.Place.Size)+"最多雇佣 "+StaffLimit(shop.Place.Size)+" 人（含待安排及已预留调入名额）":null;
        }
        private static void AddShopEmployee(OwnedShop shop,EmployeeRole role,Employee person)
        {
            if(role==EmployeeRole.Chef||role==EmployeeRole.Waiter||role==EmployeeRole.Appraiser||role==EmployeeRole.PetKeeper)shop.AdditionalEmployees.Add(new AdditionalShopEmployee{Role=role,Person=person});
            else if(EmployeeAt(shop,role)==null)PutEmployee(shop,role,person);
            else shop.AdditionalEmployees.Add(new AdditionalShopEmployee{Role=role,Person=person});
        }
        private Employee[] ShopCheckoutWorkers(OwnedShop shop)
        {
            var cashiers=ShopStaff(shop,EmployeeRole.Cashier).Where(e=>CityEmployeeAvailable(e,shop)).ToArray();
            return cashiers.Length>0?cashiers:ShopStaff(shop,EmployeeRole.Assembler).Where(e=>CityEmployeeAvailable(e,shop)).ToArray();
        }
        private static void RemoveShopEmployee(OwnedShop shop,EmployeeRole role,Employee person)
        {
            if(EmployeeAt(shop,role)!=null&&EmployeeAt(shop,role)==person)
            {
                var next=shop.AdditionalEmployees.FirstOrDefault(e=>e.Role==role);
                PutEmployee(shop,role,next?.Person);
                if(next!=null)shop.AdditionalEmployees.Remove(next);
            }
            else shop.AdditionalEmployees.RemoveAll(e=>e.Person==person);
            if (role == EmployeeRole.Assembler) ClearUnstaffedAssembly(shop);
        }
        private void SaveAdditionalStaff(SaveData data)
        {
            data.additionalShopEmployees=MyShops.SelectMany(shop=>shop.AdditionalEmployees.Select(e=>new SavedAdditionalEmployee{
                ShopId=shop.Place.Id,Role=(int)e.Role,Person=ToSave(e.Person)})).ToArray();
            data.employeeHireShops=_employeeHireShops.Select(p=>new SavedHireShop{EmployeeId=p.Key,ShopId=p.Value}).ToArray();
        }
        private void LoadAdditionalStaff(SaveData data)
        {
            foreach(var shop in _shops)shop.AdditionalEmployees.Clear();
            foreach(var entry in data.additionalShopEmployees??Array.Empty<SavedAdditionalEmployee>())
            {
                var shop=ShopAt(entry.ShopId);var role=(EmployeeRole)entry.Role;
                if(shop==null||entry.Person==null||role==EmployeeRole.Unassigned||!Enum.IsDefined(typeof(EmployeeRole),role))continue;
                var person=FromSave(entry.Person);
                if(Employees.Any(e=>e.Person.Id==person.Id))continue;
                AddShopEmployee(shop,role,person);
            }
            _employeeHireShops.Clear();
            foreach(var entry in data.employeeHireShops??Array.Empty<SavedHireShop>())
                if(entry?.EmployeeId!=null&&ShopAt(entry.ShopId)!=null)_employeeHireShops[entry.EmployeeId]=entry.ShopId;
        }
    }
}

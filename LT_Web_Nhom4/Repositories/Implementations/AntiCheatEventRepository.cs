using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Repositories.Interfaces;

namespace LT_Web_Nhom4.Repositories.Implementations
{
    public class AntiCheatEventRepository : EfRepository<AntiCheatEvent>, IAntiCheatEventRepository
    {
        public AntiCheatEventRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using LT_Web_Nhom4.Repositories.Interfaces;

namespace LT_Web_Nhom4.Repositories.Implementations
{
    public class ClassMemberRepository : EfRepository<ClassMember>, IClassMemberRepository
    {
        public ClassMemberRepository(ApplicationDbContext context) : base(context)
        {
        }
    }
}

using LT_Web_Nhom4.Controllers;
using LT_Web_Nhom4.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LT_Web_Nhom4.Services.Interfaces;
using ClassEntity = LT_Web_Nhom4.Models.Class;

namespace LT_Web_Nhom4.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ClassesController : CrudController<ClassEntity>
    {
        private readonly IUniqueCodeGenerator _codeGenerator;

        public ClassesController(ApplicationDbContext context, IUniqueCodeGenerator codeGenerator) : base(context)
        {
            _codeGenerator = codeGenerator;
        }

        protected override async Task OnCreatingAsync(ClassEntity entity)
        {
            entity.Code = await _codeGenerator.GenerateClassCodeAsync();
        }
    }
}

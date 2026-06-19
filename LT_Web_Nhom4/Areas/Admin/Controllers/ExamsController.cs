using LT_Web_Nhom4.Controllers;
using LT_Web_Nhom4.Data;
using LT_Web_Nhom4.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LT_Web_Nhom4.Services.Interfaces;

namespace LT_Web_Nhom4.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ExamsController : CrudController<Exam>
    {
        private readonly IUniqueCodeGenerator _codeGenerator;

        public ExamsController(ApplicationDbContext context, IUniqueCodeGenerator codeGenerator) : base(context)
        {
            _codeGenerator = codeGenerator;
        }

        protected override async Task OnCreatingAsync(Exam entity)
        {
            entity.Code = await _codeGenerator.GenerateExamCodeAsync();
        }
    }
}

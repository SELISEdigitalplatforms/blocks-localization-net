using FluentValidation;

namespace DomainService.Services
{
    public class ModuleValidator : AbstractValidator<Module>
    {
        public ModuleValidator()
        {
            RuleFor(module => module.ModuleName)
                .NotEmpty().WithMessage("Module name is required.")
                .Length(3, 100).WithMessage("Module name must be between 3 and 100 characters long.");
        }
    }
}
using FluentValidation;
using Phisio.Application.Clinics;

namespace Phisio.Application.Clinics.Validators;

public class CreateClinicDtoValidator : AbstractValidator<CreateClinicDto>
{
    public CreateClinicDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام کلینیک الزامی است.")
            .MaximumLength(200).WithMessage("نام کلینیک حداکثر ۲۰۰ کاراکتر باشد.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("آدرس کلینیک الزامی است.")
            .MaximumLength(500).WithMessage("آدرس کلینیک حداکثر ۵۰۰ کاراکتر باشد.");

        RuleFor(x => x.PhoneNumbers)
            .NotEmpty().WithMessage("حداقل یک شماره تلفن برای مطب الزامی است.");

        RuleForEach(x => x.PhoneNumbers)
            .NotEmpty().WithMessage("شماره تلفن نمی‌تواند خالی باشد.")
            .MaximumLength(20).WithMessage("شماره تلفن حداکثر ۲۰ کاراکتر باشد.")
            .Matches(@"^\+?[0-9\s\-()]+$")
            .WithMessage("فرمت شماره تلفن نامعتبر است.");
    }
}

public class UpdateClinicDtoValidator : AbstractValidator<UpdateClinicDto>
{
    public UpdateClinicDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام کلینیک الزامی است.")
            .MaximumLength(200).WithMessage("نام کلینیک حداکثر ۲۰۰ کاراکتر باشد.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("آدرس کلینیک الزامی است.")
            .MaximumLength(500).WithMessage("آدرس کلینیک حداکثر ۵۰۰ کاراکتر باشد.");

        RuleFor(x => x.PhoneNumbers)
            .NotEmpty().WithMessage("حداقل یک شماره تلفن برای مطب الزامی است.");

        RuleForEach(x => x.PhoneNumbers)
            .NotEmpty().WithMessage("شماره تلفن نمی‌تواند خالی باشد.")
            .MaximumLength(20).WithMessage("شماره تلفن حداکثر ۲۰ کاراکتر باشد.")
            .Matches(@"^\+?[0-9\s\-()]+$")
            .WithMessage("فرمت شماره تلفن نامعتبر است.");
    }
}

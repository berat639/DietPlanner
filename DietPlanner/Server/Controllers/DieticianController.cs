﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using AutoMapper;

using DietPlanner.DTO.Auth;
using DietPlanner.DTO.Person;
using DietPlanner.DTO.Response;
using DietPlanner.Server.BLL.ExtensionMethods;
using DietPlanner.Server.BLL.Helpers;
using DietPlanner.Server.BLL.Interfaces;
using DietPlanner.Server.Entities.Concrete;
using DietPlanner.Server.Entities.Enums;
using DietPlanner.Server.Filters;
using DietPlanner.Shared.DesignPatterns.FluentFactory;
using DietPlanner.Shared.ExtensionMethods;
using DietPlanner.Shared.StringInfo;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DietPlanner.Server.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [CustomAuthorize]
    public class DieticianController : ControllerBase
    {
        private readonly IGenericQueryService<Patient> genericPatientService;
        private readonly IGenericCommandService<Patient> genericPatientCommandService;
        private readonly IPersonService personService;
        private readonly IRoleService roleService;
        private readonly IMessageService messageService;
        private readonly IMapper mapper;

        public DieticianController(
            IGenericQueryService<Patient> genericPatientService,
            IGenericCommandService<Patient> genericPatientCommandService,
            IPersonService personService,
            IRoleService roleService,
            IMessageService messageService,
            IMapper mapper)
        {
            this.genericPatientService = genericPatientService;
            this.genericPatientCommandService = genericPatientCommandService;
            this.personService = personService;
            this.roleService = roleService;
            this.messageService = messageService;
            this.mapper = mapper;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllPatient()
        {
            return Response<IEnumerable<UserDto>>
                  .Success(await genericPatientService.GetAllAsync<UserDto>(), StatusCodes.Status200OK)
                  .CreateResponseInstance();
        }

        [HttpPost]
        public async Task<IActionResult> CreatePatient(UserCreateDto dto)
        {
            UserDto found = await personService.GetPersonByIdentityNumber(PersonType.Patient, dto.IdentityNumber);
            if (found.IsNotNull())
                return Response<UserDto>.Fail(
                           statusCode: StatusCodes.Status400BadRequest,
                           isShow: true,
                           path: "[POST] api/admin/createPatient",
                           errors: "Girilen kimlik no ile kayıtlı bir hasta zaten mevcut"
                           )
                    .CreateResponseInstance();
            Role patientRole = await roleService.GetRoleByName(RoleInfo.Patient);

            string autoGeneratedPassword = PasswordHelper.CreateRandomPassword();

            FluentFactory<UserCreateDto>.Init(dto)
                .GiveAValue(x => x.RoleId, patientRole.Id)
                .GiveAValue(x => x.CreateUserId, Response.GetUserId())
                .GiveAValue(x => x.Password, autoGeneratedPassword)
                .Use(obj => obj.Password = BCrypt.Net.BCrypt.HashPassword(dto.Password));

            Patient patient = await genericPatientCommandService.AddAsync(dto);
            await genericPatientCommandService.Commit();
            UserDto returnModel = mapper.Map<UserDto>(patient);

            ThreadPool.QueueUserWorkItem(async callBack =>
            {
                await messageService.SendWelcomeAsync(new()
                {
                    FirstName = returnModel.FirstName,
                    LastName = returnModel.LastName,
                    Password = autoGeneratedPassword,
                    ToEmail = returnModel.Email
                });
            });

            return Response<UserDto>
                .Success(returnModel, StatusCodes.Status201Created)
                .CreateResponseInstance();
        }
    }
}
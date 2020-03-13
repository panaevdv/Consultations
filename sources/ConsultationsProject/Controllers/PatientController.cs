﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ConsultationsProject.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ConsultationsProject.Controllers
{
    /// <summary>
    /// Контроллер, ответственный за обработку запросов с пациентами.
    /// </summary>
    [Route("patient-management/patients")]
    public class PatientController : Controller
    {
        /// <summary>
        /// Логгер.
        /// </summary>
        private readonly ILogger logger;

        /// <summary>
        /// Конструктор контроллера.
        /// </summary>
        /// <param name="logger">Логгер.</param>
        public PatientController(ILogger<PatientController> logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Метод, возвращающий представление для добавления нового пациента.
        /// </summary>
        /// <returns>
        /// Представление для добавления нового пациента.
        /// </returns>
        [HttpGet]
        public IActionResult Add()
        {
            return View();
        }

        /// <summary>
        /// Метод, ответственный за добавление нового пациента в БД.
        /// </summary>
        /// <param name="patient">Данные пациента.</param>
        /// <returns>
        /// Страницу с ошибкой, если не было данных о пациенте в запросе.
        /// Представление со страницей добавления пациента с введенными ранее данными, если дата рождения не пройдет валидацию.
        /// Представление главной страницы с сообщением об успешном добавлении пациента.
        /// </returns>
        [HttpPost]
        public IActionResult Add(Patient patient)
        {
            if (ModelState.IsValid)
            {
                using (PatientsContext db = new PatientsContext())
                {
                    if (patient == null)
                    {
                        logger.LogError("При добавлении пациента произошла ошибка связывания модели.");
                        return View("Error",
                            new ErrorViewModel { Message = "При добавлении пациента произошла ошибка связывания модели" });
                    }

                    if (patient.BirthDate < DateTime.Parse("01/01/1880") ||
                        patient.BirthDate > DateTime.Now.AddYears(1))
                    {
                        logger.LogError($"При добавлении нового пациента произошла ошибка: " +
                            $"Недопустимая дата: {patient.BirthDate}");
                        ModelState.AddModelError("BirthDate", $"Дата рождения должна быть в промежутке" +
                            $" от {DateTime.Parse("01/01/1880").ToString("d")} до {DateTime.Now.AddYears(1).ToString("d")}");
                        return View(patient);
                    }

                    patient.PensionNumber = Regex.Replace(patient.PensionNumber, "[^0-9]", "");
                    var result = db.Patients
                        .Where(x => x.PensionNumber == patient.PensionNumber)
                        .FirstOrDefault();
                    if (result == null)
                    {
                        db.Patients.Add(patient);
                        db.SaveChanges();
                        logger.LogInformation($"Добавлен новый пациент в базу данных. СНИЛС: {patient.PensionNumber}.");
                        return RedirectToAction("Index", "Home", new { message = "Пациент успешно добавлен" });
                    }
                    else
                    {
                        logger.LogError($"При добавлении нового пациента " +
                            $"обнаружен пациент с идентичным СНИЛС = {patient.PensionNumber}.");
                        ModelState.AddModelError("PensionNumber", "Пациент с таким СНИЛС уже существует");
                    }
                }
            }
            return View(patient);
        }

        /// <summary>
        /// Метод, возвращающий представление с информацией о пациенте.
        /// </summary>
        /// <param name="id">Уникальный id пациента.</param>
        /// <param name="message">Сообщение об успешном добавлении/редактировании/удалении консультации пациента.</param>
        /// <returns>
        /// Страницу с ошибкой, если пациент не найден в БД.
        /// Представление с информацией о пациенте.
        /// </returns>
        [HttpGet("{id}")]
        public IActionResult Get(int id, string message = "")
        {
            ViewBag.Message = message;
            using (PatientsContext db = new PatientsContext())
            {
                var patient = db.Patients
                    .Include(x => x.Consultations)
                    .Where(x => x.PatientId == id)
                    .FirstOrDefault();
                if (patient != null)
                {
                    logger.LogInformation($"Запрос {HttpContext.Request.Query} вернул пациента с id {id}");
                    return View(patient);
                }
                else
                {
                    logger.LogError($"При получении страницы пациента произошла ошибка:" +
                        $" не найден пациент с id = {id}. Запрос: {HttpContext.Request.Query}.");
                    return View("Error",
                        new ErrorViewModel { Message = $"При получении страницы пациента произошла ошибка:" +
                        $" не найден пациент с id = {id}" });
                }
            }
        }

        /// <summary>
        /// Метод, использующийся для поиска пациентов в БД по имени или СНИЛС.
        /// </summary>
        /// <param name="name">ФИО пациента.</param>
        /// <param name="pension">СНИЛС пациента.</param>
        /// <returns>
        /// Частичное представление со списком пациентов, которые удовлетворяют заданным поисковым критериям.
        /// </returns>
        [HttpGet("{name}/{pension}")]
        public IActionResult List(string name, string pension)
        {
            using (PatientsContext db = new PatientsContext())
            {
                var patients = db.Patients.AsEnumerable();
                if (!String.IsNullOrEmpty(name))
                {
                    patients = patients.Where(x => EF.Functions.Like
                    (String.Concat(x.FirstName, " ", x.LastName, " ", x.Patronymic), "%" + name + "%"));
                }
                if (!String.IsNullOrEmpty(pension))
                {
                    patients = patients.Where(x => EF.Functions.Like(x.PensionNumber, pension + "%"));
                }
                var result = patients.ToList();
                logger.LogInformation($"Поисковой запрос {HttpContext.Request.Query} вернул {result.Count} кол-во пациентов.");
                return PartialView(result);
            }
        }

        /// <summary>
        /// Метод, возвращающий представление с данными пациента для редактирования.
        /// </summary>
        /// <param name="id">Уникальный id пациента.</param>
        /// <returns>
        /// Представление с информацией о пациенте для редактирования.
        /// </returns>
        
        
        [HttpGet("{id}")]
        public IActionResult Edit(int id)
        {
            using (PatientsContext db = new PatientsContext())
            {
                var patient = db.Patients.Find(id);
                if (patient != null)
                {
                    logger.LogInformation($"Пациент с id = {id} был изменен.");
                    return View(patient);
                }
                logger.LogError($"При попытке изменения пациент с id = {id} был не найден в базе данных");
                return View("Error",
                    new ErrorViewModel { Message = $"При попытке изменения пациент с id = {id} был не найден в базе данных" });
            }
        }

        /// <summary>
        /// Метод, ответственный за редактирование данных пациента в БД.
        /// </summary>
        /// <param name="id">Уникальный id пациента.</param>
        /// <param name="patient">Измененные данные пациента.</param>
        /// <returns>
        /// Страницу с ошибкой, если не было данных о пациенте в запросе.
        /// Страницу с ошибкой, если пациент был удален из БД.
        /// Представление со страницей добавления пациента с введенными ранее данными, если дата рождения не пройдет валидацию.
        /// Представление со страницей добавления пациента с введенными ранее данными, если СНИЛС не является уникальным.
        /// Представление с информацией о пациенте с сообщением об успешном изменении.
        /// </returns>
        [HttpPost("{id}")]
        public IActionResult Edit(int id, Patient patient)
        {
            using (PatientsContext db = new PatientsContext())
            {
                if (patient == null)
                {
                    logger.LogError($"При изменении пациента с id = {id} произошла ошибка связывания модели");
                    return View("Error",
                        new ErrorViewModel { Message = $"При изменении пациента с id = {id} произошла ошибка связывания модели"});
                }
                var _patient = db.Patients.Find(id);
                if (_patient != null)
                {
                    if (patient.BirthDate < DateTime.Parse("01/01/1880") ||
                        patient.BirthDate > DateTime.Now.AddYears(1))
                    {
                        logger.LogError($"При изменения пациента с id = {id} произошла ошибка: " +
                            $"Недопустимая дата: {patient.BirthDate}");
                        ModelState.AddModelError("BirthDate", $"Дата рождения должна быть в промежутке" +
                            $" от {DateTime.Parse("01/01/1880").ToString("d")} до {DateTime.Now.AddYears(1).ToString("d")}");
                        return View(patient);
                    }

                    patient.PensionNumber = Regex.Replace(patient.PensionNumber, "[^0-9]", "");
                    var pensionCheck = db.Patients
                        .Where(x => x.PensionNumber == patient.PensionNumber)
                        .FirstOrDefault();
                    if (pensionCheck == null||pensionCheck.PatientId==id)
                    {
                        db.Entry(_patient).CurrentValues.SetValues(patient);
                        db.SaveChanges();
                        logger.LogInformation($"Пациент с id = {id} был изменен");
                        return RedirectToAction("Get", "Patient",
                            new { id = patient.PatientId, message = "Пациент успешно изменен"});
                    }
                    else
                    {
                        logger.LogWarning($"При изменении пациента с id = {id} произошла ошибка: " +
                            $"Был обнаружен пациент с таким же СНИЛС = {patient.PensionNumber}");
                        ModelState.AddModelError("PensionNumber", "Пациент с таким СНИЛС уже существует");
                        return View(patient);
                    }
                }
                logger.LogError($"При попытке изменения пациент с id = {id} был не найден в базе данных");
                return View("Error",
                    new ErrorViewModel { Message = $"При попытке изменения пациент с id = {id} был не найден в базе данных" });
            }
        }

        /// <summary>
        /// Метод, ответственный за удаление пациента из БД.
        /// </summary>
        /// <param name="id">Уникальный id пациента.</param>
        /// <returns>
        /// JSON со статусом false и сообщением об ошибке, если пациент не найден в БД.
        /// JSON со статусом true и сообщением об успешном удалении.
        /// </returns>
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            using (PatientsContext db = new PatientsContext())
            {
                var patient = db.Patients.Find(id);
                if (patient != null)
                {
                    logger.LogInformation($"Пациент с id = {id} был удален из базы данных");
                    db.Remove(patient);
                    db.SaveChanges();
                    return Json(new { success = "true", message = "Пациент успешно удален" });
                }
                logger.LogError($"При попытке удаления пациент с id = {id} был не найден в базе данных");
                return Json(new { success = "false", message = $"При попытке удаления пациент с id = {id} был не найден в базе данных" });
            }
        }
    }
}
﻿using System;
using System.Linq;
using System.Web.Mvc;
using AutoMapper;
using NuSurvey.Core.Domain;
using NuSurvey.Web.Controllers.Filters;
using UCDArch.Core.PersistanceSupport;
using UCDArch.Core.Utils;
using MvcContrib;
using UCDArch.Web.Helpers;

namespace NuSurvey.Web.Controllers
{
    /// <summary>
    /// Controller for the Category class
    /// </summary>
    [Admin]
    public class CategoryController : ApplicationController
    {
	    private readonly IRepository<Category> _categoryRepository;

        public CategoryController(IRepository<Category> categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }
    

        ////
        //// GET: /Category/Details/5
        //public ActionResult Details(int id)
        //{
        //    var category = _categoryRepository.GetNullableById(id);

        //    if (category == null) return RedirectToAction("Index");

        //    return View(category);
        //}

        /// <summary>
        /// GET: /Category/Create
        /// </summary>
        /// <param name="id">Survey Id</param>
        /// <returns></returns>
        public ActionResult Create(int id)
        {
            var survey = Repository.OfType<Survey>().GetNullableById(id);
            if (survey == null)
            {
                return this.RedirectToAction<SurveyController>(a => a.Index());
            }

			var viewModel = CategoryViewModel.Create(Repository, survey);
            
            return View(viewModel);
        } 

        /// <summary>
        /// POST: /Category/Create
        /// </summary>
        /// <param name="id">Survey Id</param>
        /// <param name="category"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult Create(int id, Category category)
        {
            var survey = Repository.OfType<Survey>().GetNullableById(id);
            if (survey == null)
            {
                return this.RedirectToAction<SurveyController>(a => a.Index());
            }

            var categoryToCreate = new Category(survey);

            Mapper.Map(category, categoryToCreate);

    

            ModelState.Clear();
            categoryToCreate.TransferValidationMessagesTo(ModelState);

            if (ModelState.IsValid)
            {
                _categoryRepository.EnsurePersistent(categoryToCreate);

                Message = "Category Created Successfully";

                return this.RedirectToAction(a => a.Edit(categoryToCreate.Id));
            }
            else
            {
				var viewModel = CategoryViewModel.Create(Repository, survey);
                viewModel.Category = categoryToCreate;

                return View(viewModel);
            }
        }

        
        /// <summary>
        /// GET: /Category/Edit/5
        /// </summary>
        /// <param name="id">Category Id</param>
        /// <returns></returns>
        public ActionResult Edit(int id)
        {
            var category = _categoryRepository.GetNullableById(id);

            if (category == null)
            {
                Message = "Category not found to edit.";
                return this.RedirectToAction<SurveyController>(a => a.Index());
            }

            var viewModel = CategoryViewModel.Create(Repository, category.Survey);
            viewModel.Category = category;

            return View(viewModel);
        }

        /// <summary>
        /// POST: /Category/Edit/5
        /// </summary>
        /// <param name="id">Category Id</param>
        /// <param name="category"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult Edit(int id, Category category)
        {
            var categoryToEdit = _categoryRepository.GetNullableById(id);

            if (categoryToEdit == null)
            {
                Message = "Category not found to edit.";
                return this.RedirectToAction<SurveyController>(a => a.Index());
            }

            Mapper.Map(category, categoryToEdit);

            ModelState.Clear();
            categoryToEdit.TransferValidationMessagesTo(ModelState);

            if (ModelState.IsValid)
            {
                _categoryRepository.EnsurePersistent(categoryToEdit);

                Message = "Category Edited Successfully";

                return this.RedirectToAction<SurveyController>(a => a.Edit(categoryToEdit.Survey.Id));
            }
            else
            {
                var viewModel = CategoryViewModel.Create(Repository, categoryToEdit.Survey);
                viewModel.Category = category;

                return View(viewModel);
            }
        }
               

    }

	/// <summary>
    /// ViewModel for the Category class
    /// </summary>
    public class CategoryViewModel
	{
        public Survey Survey { get; set; }
		public Category Category { get; set; }        
 
		public static CategoryViewModel Create(IRepository repository, Survey survey)
		{
			Check.Require(repository != null, "Repository must be supplied");
            Check.Require(survey != null);
			
			var viewModel = new CategoryViewModel {Survey = survey, Category = new Category(survey)};            
 
			return viewModel;
		}
	}
}

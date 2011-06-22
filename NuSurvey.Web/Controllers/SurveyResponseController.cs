﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web.Mvc;
using NuSurvey.Core.Domain;
using NuSurvey.Web.Controllers.Filters;
using NuSurvey.Web.Services;
using UCDArch.Core.PersistanceSupport;
using UCDArch.Core.Utils;
using MvcContrib;
using UCDArch.Web.Helpers;

namespace NuSurvey.Web.Controllers
{
    /// <summary>
    /// Controller for the SurveyResponse class
    /// </summary>
    [Authorize]
    public class SurveyResponseController : ApplicationController
    {
	    private readonly IRepository<SurveyResponse> _surveyResponseRepository;
        private readonly IScoreService _scoreService;

        public SurveyResponseController(IRepository<SurveyResponse> surveyResponseRepository, IScoreService scoreService)
        {
            _surveyResponseRepository = surveyResponseRepository;
            _scoreService = scoreService;
        }

        /// <summary>
        /// #1
        /// GET: /SurveyResponse/
        /// </summary>
        /// <returns></returns>
        public ActionResult Index()
        {
            var isPublic = !(CurrentUser.IsInRole(RoleNames.User) || CurrentUser.IsInRole(RoleNames.Admin));
            var viewModel = ActiveSurveyViewModel.Create(Repository, isPublic);

            return View(viewModel);
        }
        
        
        /// <summary>
        /// #2
        /// Called from the Survey Details.
        /// GET: /SurveyResponse/Details/5
        /// </summary>
        /// <param name="id">SurveyResponse Id</param>
        /// <returns></returns>
        [Admin]
        public ActionResult Details(int id)
        {
            var surveyResponse = _surveyResponseRepository.GetNullableById(id);

            if (surveyResponse == null)
            {
                Message = "Survey Response Details Not Found.";
                return this.RedirectToAction<ErrorController>(a => a.Index());
                //return RedirectToAction("Index");
            }

            var viewModel = SurveyReponseDetailViewModel.Create(Repository, surveyResponse);

            return View(viewModel);
        }

        /// <summary>
        /// #3
        /// Start or continue a survey with one question at a time
        /// GET: /SurveyResponse/StartSurvey/5
        /// </summary>
        /// <param name="id">Survey ID</param>
        /// <returns></returns>
        public ActionResult StartSurvey(int id)
        {
            var survey = Repository.OfType<Survey>().GetNullableById(id);
            if (survey == null || !survey.IsActive)
            {
                Message = "Survey not found or not active.";
                return this.RedirectToAction<ErrorController>(a => a.Index());
            }

            #region Check To See if there are enough available Categories
            if (GetCountActiveCategoriesWithScore(survey) < 3)
            {
                Message = "Survey does not have enough active categories to complete survey.";
                return this.RedirectToAction<ErrorController>(a => a.Index());
            }
            #endregion Check To See if there are enough available Categories

            var cannotContinue = false;

            var pendingExists = _surveyResponseRepository.Queryable
                .Where(a => a.Survey.Id == id && a.IsPending && a.UserId == CurrentUser.Identity.Name).FirstOrDefault();
            if (pendingExists != null)
            {
                foreach (var answer in pendingExists.Answers)
                {
                    if (!answer.Category.IsCurrentVersion)
                    {
                        Message =
                            "The unfinished survey's questions have been modifed. Unable to continue. Delete survey and start again.";
                        cannotContinue = true;
                        break;
                    }
                }
                if (!cannotContinue)
                {
                    Message = "Unfinished survey found.";
                }
            }

            var viewModel = SingleAnswerSurveyResponseViewModel.Create(Repository, survey, pendingExists);
            viewModel.CannotContinue = cannotContinue;

            return View(viewModel);

        }

        private int GetCountActiveCategoriesWithScore(Survey survey)
        {
            var count = 0;
            foreach (var category in survey.Categories.Where(a => !a.DoNotUseForCalculations && a.IsActive && a.IsCurrentVersion))
            {
                var totalMax = Repository.OfType<CategoryTotalMaxScore>().GetNullableById(category.Id);
                if (totalMax == null) //No Questions most likely
                {
                    continue;
                }
                count++;
                if (count > 3)
                {
                    break;
                }
            }
            return count;
        }

        /// <summary>
        /// #4
        /// Start or continue a survey with one question at a time
        /// POST: 
        /// </summary>
        /// <param name="id">Survey Id</param>
        /// <param name="surveyResponse"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult StartSurvey(int id, SurveyResponse surveyResponse)
        {
            var survey = Repository.OfType<Survey>().GetNullableById(id);
            if (survey == null || !survey.IsActive)
            {
                Message = "Survey not found or not active.";
                return this.RedirectToAction<ErrorController>(a => a.Index());
            }

            surveyResponse.IsPending = true;
            surveyResponse.Survey = survey;
            surveyResponse.UserId = CurrentUser.Identity.Name;

            ModelState.Clear();
            surveyResponse.TransferValidationMessagesTo(ModelState);

            if (ModelState.IsValid)
            {
                _surveyResponseRepository.EnsurePersistent(surveyResponse);

                return this.RedirectToAction(a => a.AnswerNext(surveyResponse.Id));
            }

            Message = "Please correct errors to continue";

            var viewModel = SingleAnswerSurveyResponseViewModel.Create(Repository, survey, null);
            viewModel.SurveyResponse = surveyResponse;

            return View(viewModel);
        }

        /// <summary>
        /// #5
        /// Load the next available question, or finalize if all questions are answered.
        /// GET: /SurveyResponse/AnswerNext/5
        /// </summary>
        /// <param name="id">SurveyResponse Id</param>
        /// <returns></returns>
        public ActionResult AnswerNext(int id)
        {
            var surveyResponse = _surveyResponseRepository.GetNullableById(id);
            if (surveyResponse == null || !surveyResponse.IsPending)
            {
                Message = "Pending survey not found";
                return this.RedirectToAction<ErrorController>(a => a.Index());
            }
            if (surveyResponse.UserId != CurrentUser.Identity.Name)
            {
                Message = "Not your survey";
                return this.RedirectToAction<ErrorController>(a => a.NotAuthorized());
            }
            var viewModel = SingleAnswerSurveyResponseViewModel.Create(Repository, surveyResponse.Survey, surveyResponse);
            if (viewModel.CurrentQuestion == null)
            {
                return this.RedirectToAction(a => a.FinalizePending(surveyResponse.Id));
            }
            return View(viewModel);
        }

        /// <summary>
        /// #6
        /// POST:
        /// </summary>
        /// <param name="id">SurveyResponse Id</param>
        /// <param name="questions"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult AnswerNext(int id, QuestionAnswerParameter questions)
        {
            var surveyResponse = _surveyResponseRepository.GetNullableById(id);
            if (surveyResponse == null || !surveyResponse.IsPending)
            {
                Message = "Pending survey not found";
                return this.RedirectToAction<ErrorController>(a => a.Index());
            }
            if (surveyResponse.UserId != CurrentUser.Identity.Name)
            {
                Message = "Not your survey";
                return this.RedirectToAction<ErrorController>(a => a.NotAuthorized());
            }

            var question = Repository.OfType<Question>().GetNullableById(questions.QuestionId);
            if (question == null)
            {
                Message = "Question survey not found";
                return this.RedirectToAction<ErrorController>(a => a.Index());
            }

            Answer answer;
            if (surveyResponse.Answers.Where(a => a.Question.Id == question.Id).Any())
            {
                answer = surveyResponse.Answers.Where(a => a.Question.Id == question.Id).First();
            }
            else
            {
                answer = new Answer(); 
            }

            questions = _scoreService.ScoreQuestion(surveyResponse.Survey.Questions.AsQueryable(), questions);
            if (questions.Invalid)
            {
                ModelState.AddModelError("Questions", questions.Message);
            }
            else
            {
                //It is valid, add the answer.
                answer.Question = question;
                answer.Category = question.Category;
                answer.OpenEndedAnswer = questions.OpenEndedNumericAnswer;
                answer.Response = Repository.OfType<Response>().GetNullableById(questions.ResponseId);
                answer.Score = questions.Score;

                surveyResponse.AddAnswers(answer);
            }

            if (ModelState.IsValid)
            {
                _surveyResponseRepository.EnsurePersistent(surveyResponse);
                return this.RedirectToAction(a => a.AnswerNext(surveyResponse.Id));
            }

            var viewModel = SingleAnswerSurveyResponseViewModel.Create(Repository, surveyResponse.Survey, surveyResponse);

            return View(viewModel);
            

        }

        /// <summary>
        /// #7
        /// Calculate the positive and two negative categories and set the pending flag to false
        /// </summary>
        /// <param name="id">SurveyResponse Id</param>
        /// <returns></returns>
        public ActionResult FinalizePending(int id)
        {
            var surveyResponse = _surveyResponseRepository.GetNullableById(id);
            if (surveyResponse == null || !surveyResponse.IsPending)
            {
                Message = "Pending survey not found";
                return this.RedirectToAction<ErrorController>(a => a.Index());
            }
            if (surveyResponse.UserId != CurrentUser.Identity.Name)
            {
                Message = "Not your survey";
                return this.RedirectToAction<ErrorController>(a => a.NotAuthorized());
            }
            var viewModel = SingleAnswerSurveyResponseViewModel.Create(Repository, surveyResponse.Survey, surveyResponse);
            if (viewModel.CurrentQuestion == null)
            {
                _scoreService.CalculateScores(Repository, surveyResponse);
                surveyResponse.IsPending = false;
                _surveyResponseRepository.EnsurePersistent(surveyResponse);
                return this.RedirectToAction(a => a.Results(surveyResponse.Id));
            }
            else
            {
                Message = "Error finalizing survey.";
                return this.RedirectToAction<ErrorController>(a => a.Index());
            }
        }


        /// <summary>
        /// #8
        /// GET:
        /// </summary>
        /// <param name="id">SurveyResponse Id</param>
        /// <param name="fromAdmin"></param>
        /// <returns></returns>
        public ActionResult DeletePending(int id, bool fromAdmin)
        {
            var surveyResponse = _surveyResponseRepository.GetNullableById(id);
            if (surveyResponse == null || !surveyResponse.IsPending)
            {
                Message = "Pending survey not found";
                return this.RedirectToAction<ErrorController>(a => a.Index());
            }
            if (!CurrentUser.IsInRole(RoleNames.Admin) && surveyResponse.UserId != CurrentUser.Identity.Name)
            {
                Message = "Not your survey";
                return this.RedirectToAction<ErrorController>(a => a.NotAuthorized());
            }
            var viewModel = SingleAnswerSurveyResponseViewModel.Create(Repository, surveyResponse.Survey, surveyResponse);
            viewModel.FromAdmin = fromAdmin;

            return View(viewModel);
        }

        /// <summary>
        /// #9
        /// POST:
        /// </summary>
        /// <param name="id">SurveyResponse Id</param>
        /// <param name="confirm"></param>
        /// <param name="fromAdmin"></param>
        /// <returns></returns>
        [HttpPost]
        public ActionResult DeletePending(int id, bool confirm, bool fromAdmin)
        {
            var surveyResponse = _surveyResponseRepository.GetNullableById(id);
            if (surveyResponse == null || !surveyResponse.IsPending)
            {
                Message = "Pending survey not found";
                return this.RedirectToAction<ErrorController>(a => a.Index());
            }
            if (!CurrentUser.IsInRole(RoleNames.Admin) && surveyResponse.UserId != CurrentUser.Identity.Name)
            {
                Message = "Not your survey";
                return this.RedirectToAction<ErrorController>(a => a.NotAuthorized());
            }

            var surveyId = surveyResponse.Survey.Id;

            if (confirm == false)
            {
                if (fromAdmin)
                {
                    return this.RedirectToAction<SurveyController>(a => a.PendingDetails(surveyId));
                }
                return this.RedirectToAction(a => a.StartSurvey(surveyId));
            }

            _surveyResponseRepository.Remove(surveyResponse);

            if (fromAdmin)
            {
                return this.RedirectToAction<SurveyController>(a => a.PendingDetails(surveyId));
            }
            return this.RedirectToAction<HomeController>(a => a.Index());

        }

        /// <summary>
        /// #10
        /// GET: /SurveyResponse/Create
        /// </summary>
        /// <param name="id">Survey Id</param>
        /// <returns></returns>
        [User]
        public ActionResult Create(int id)
        {
            var survey = Repository.OfType<Survey>().GetNullableById(id);
            if (survey == null || !survey.IsActive)
            {
                Message = "Survey not found or not active.";
                return this.RedirectToAction<ErrorController>(a => a.Index());
            }

            #region Check To See if there are enough available Categories
            if (GetCountActiveCategoriesWithScore(survey) < 3)
            {
                Message = "Survey does not have enough active categories to complete survey.";
                return this.RedirectToAction<ErrorController>(a => a.Index());
            }
            #endregion Check To See if there are enough available Categories

			var viewModel = SurveyResponseViewModel.Create(Repository, survey);
            
            return View(viewModel);
        } 

        ///// <summary>
        ///// #11
        ///// POST: /SurveyResponse/Create
        ///// </summary>
        ///// <param name="id"></param>
        ///// <param name="surveyResponse"></param>
        ///// <param name="questions"></param>
        ///// <returns></returns>
        //[HttpPost]
        //[User]
        //public ActionResult CreateOld(int id, SurveyResponse surveyResponse, QuestionAnswerParameter[] questions)
        //{
        //    var survey = Repository.OfType<Survey>().GetNullableById(id);
        //    if (survey == null || !survey.IsActive)
        //    {
        //        Message = "Survey not found or not active.";
        //        return this.RedirectToAction<ErrorController>(a => a.Index());
        //    }

        //    var surveyResponseToCreate = new SurveyResponse(survey);
        //    if (questions == null)
        //    {
        //        questions = new QuestionAnswerParameter[0];
        //    }

        //    TransferValues(surveyResponse, surveyResponseToCreate, questions);
        //    surveyResponseToCreate.UserId = CurrentUser.Identity.Name;

        //    if (survey.Questions.Where(a => a.IsActive && a.Category.IsActive && a.Category.IsCurrentVersion).Count() != questions.Count())
        //    {
        //        Message = "You must answer all survey questions.";
        //    }
        //    ModelState.Clear();
        //    surveyResponseToCreate.TransferValidationMessagesTo(ModelState);

        //    for (int i = 0; i < questions.Count(); i++)
        //    {
        //        var i1 = i;
        //        if (!surveyResponseToCreate.Answers.Where(a => a.Question.Id == questions[i1].QuestionId).Any())
        //        {
        //            if (survey.Questions.Where(a => a.Id == questions[i1].QuestionId).Single().IsOpenEnded)
        //            {
        //                if (string.IsNullOrWhiteSpace(questions[i1].Answer))
        //                {
        //                    ModelState.AddModelError(string.Format("Questions[{0}]", i1), "Numeric answer to Question is required"); 
        //                }
        //                else
        //                {
        //                    ModelState.AddModelError(string.Format("Questions[{0}]", i1), "Answer must be a number");  
        //                }                 
        //            }
        //            else
        //            {
        //                ModelState.AddModelError(string.Format("Questions[{0}]", i1), "Answer is required");
        //            }
        //        }
        //    }


        //    if (ModelState.IsValid)
        //    {
        //        _scoreService.CalculateScores(Repository, surveyResponseToCreate);

        //        _surveyResponseRepository.EnsurePersistent(surveyResponseToCreate);

        //        Message = "SurveyResponse Created Successfully";

        //        return this.RedirectToAction<SurveyResponseController>(a => a.Results(surveyResponseToCreate.Id));
        //    }
        //    else
        //    {
        //        //foreach (var modelState in ModelState.Values.Where(a => a.Errors.Count() > 0))
        //        //{
        //        //    var x = modelState;
        //        //}
        //        var viewModel = SurveyResponseViewModel.Create(Repository, survey);
        //        viewModel.SurveyResponse = surveyResponse;
        //        viewModel.SurveyAnswers = questions;

        //        return View(viewModel);
        //    }
        //}


        /// <summary>
        /// #11
        /// POST: /SurveyResponse/Create
        /// </summary>
        /// <param name="id"></param>
        /// <param name="surveyResponse"></param>
        /// <param name="questions"></param>
        /// <returns></returns>
        [HttpPost]
        [User]
        public ActionResult Create(int id, SurveyResponse surveyResponse, QuestionAnswerParameter[] questions)
        {
            var survey = Repository.OfType<Survey>().GetNullableById(id);
            if (survey == null || !survey.IsActive)
            {
                Message = "Survey not found or not active.";
                return this.RedirectToAction<ErrorController>(a => a.Index());
            }

            var surveyResponseToCreate = new SurveyResponse(survey);
            if (questions == null)
            {
                questions = new QuestionAnswerParameter[0];
            }

            ModelState.Clear();            

            surveyResponseToCreate.StudentId = surveyResponse.StudentId;
            surveyResponseToCreate.UserId = CurrentUser.Identity.Name;
            var length = questions.Length;
            for (int i = 0; i < length; i++)
            {
                var question = Repository.OfType<Question>().GetNullableById(questions[i].QuestionId);
                Check.Require(question != null, string.Format("Question not found./n SurveyId: {0}/n QuestionId: {1}/n Question #: {2}", id, questions[i].QuestionId, i));
                Check.Require(question.Category.IsActive, string.Format("Related Category is not active for question Id {0}", questions[i].QuestionId));
                Check.Require(question.Category.IsCurrentVersion, string.Format("Related Category is not current version for question Id {0}", questions[i].QuestionId));

                Answer answer;
                if (surveyResponseToCreate.Answers.Where(a => a.Question.Id == question.Id).Any())
                {
                    answer = surveyResponseToCreate.Answers.Where(a => a.Question.Id == question.Id).First();
                }
                else
                {
                    answer = new Answer();
                }

                questions[i] = _scoreService.ScoreQuestion(surveyResponseToCreate.Survey.Questions.AsQueryable(), questions[i]);
                if (questions[i].Invalid)
                {
                    ModelState.AddModelError(string.Format("Questions[{0}]", i), questions[i].Message);
                }

                answer.Question = question;
                answer.Category = question.Category;
                answer.OpenEndedAnswer = questions[i].OpenEndedNumericAnswer;
                answer.Response = Repository.OfType<Response>().GetNullableById(questions[i].ResponseId);
                answer.Score = questions[i].Score;

                surveyResponseToCreate.AddAnswers(answer);
            }


            surveyResponseToCreate.TransferValidationMessagesTo(ModelState);

            if (survey.Questions.Where(a => a.IsActive && a.Category.IsActive && a.Category.IsCurrentVersion).Count() != questions.Count())
            {
                Message = "You must answer all survey questions.";
            }


            if (ModelState.IsValid)
            {
                _scoreService.CalculateScores(Repository, surveyResponseToCreate);

                _surveyResponseRepository.EnsurePersistent(surveyResponseToCreate);

                Message = "SurveyResponse Created Successfully";

                return this.RedirectToAction<SurveyResponseController>(a => a.Results(surveyResponseToCreate.Id));
            }
            else
            {
                //foreach (var modelState in ModelState.Values.Where(a => a.Errors.Count() > 0))
                //{
                //    var x = modelState;
                //}
                var viewModel = SurveyResponseViewModel.Create(Repository, survey);
                viewModel.SurveyResponse = surveyResponse;
                viewModel.SurveyAnswers = questions;

                return View(viewModel);
            }
        }

        /// <summary>
        /// Get: /SurveyResponse/Results
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult Results(int id)
        {
            var surveyResponse = _surveyResponseRepository.GetNullableById(id);
            if (surveyResponse == null)
            {
                Message = "Not Found";
                return this.RedirectToAction<ErrorController>(a => a.Index());
            }
            if (!CurrentUser.IsInRole(RoleNames.Admin))
            {
                if (surveyResponse.UserId.ToLower() != CurrentUser.Identity.Name.ToLower())
                {
                    return this.RedirectToAction<ErrorController>(a => a.NotAuthorized());
                }
            }

            return View(surveyResponse);
        }




        
        /// <summary>
        /// Transfer editable values from source to destination
        /// </summary>
        private static void TransferValues(SurveyResponse source, SurveyResponse destination, QuestionAnswerParameter[] questions)
        {
			//Recommendation: Use AutoMapper
			//Mapper.Map(source, destination)

            destination.StudentId = source.StudentId;
            var queryableQuestions = questions.AsQueryable();
            foreach (var question in destination.Survey.Questions)
            {
                Question question1 = question;
                var passedQuestion = queryableQuestions.Where(a => a.QuestionId == question1.Id).SingleOrDefault();
                if (passedQuestion == null)
                {
                    continue;
                }
                var answer = new Answer();
                answer.Question = question;
                answer.Category = question.Category;                
                if (answer.Question.IsOpenEnded)
                {
                    #region Open Ended Logic

                    int number;
                    if (Int32.TryParse(passedQuestion.Answer, out number))
                    {
                        answer.OpenEndedAnswer = number;
                        answer.Response = answer.Question.Responses.Where(a => a.Value == number.ToString()).FirstOrDefault();
                        if (answer.Response == null)
                        {
                            var highResponse = answer.Question.Responses.Where(a => a.Value.Contains("+")).FirstOrDefault();
                            if (highResponse != null)
                            {
                                int highValue;
                                if (Int32.TryParse(highResponse.Value, out highValue))
                                {
                                    if (number >= highValue)
                                    {
                                        answer.Response = highResponse;
                                    }
                                }
                            }
                        }
                        if (answer.Response == null)
                        {
                            var lowResponse = answer.Question.Responses.Where(a => a.Value.Contains("-")).FirstOrDefault();
                            if (lowResponse != null)
                            {
                                int lowValue;
                                if (Int32.TryParse(lowResponse.Value, out lowValue))
                                {
                                    if (number <= Math.Abs(lowValue))
                                    {
                                        answer.Response = lowResponse;
                                    }
                                }
                            }
                        }                        
                    }
                    else
                    {
                        continue;
                    }
                    #endregion Open Ended Logic
                }
                else
                {
                    answer.Response = question1.Responses.Where(a => a.Id == passedQuestion.ResponseId).FirstOrDefault();
                }
                if (answer.Category.DoNotUseForCalculations && answer.Response == null && answer.OpenEndedAnswer != null)
                {
                    answer.Score = 0;
                    destination.AddAnswers(answer);
                }
                if (answer.Response != null)
                {
                    if (answer.Question.Category.DoNotUseForCalculations)
                    {
                        answer.Score = 0;
                    }
                    else
                    {
                        answer.Score = answer.Response.Score;
                    }
                    destination.AddAnswers(answer);
                }
                
            }      
        }
    }

    public class ActiveSurveyViewModel
    {
        public IEnumerable<Survey> Surveys { get; set; }
        public bool IsPublic { get; set; }

        public static ActiveSurveyViewModel Create(IRepository repository, bool isPublic)
        {
            Check.Require(repository != null, "Repository must be supplied");

            var viewModel = new ActiveSurveyViewModel { IsPublic = isPublic};
            viewModel.Surveys = repository.OfType<Survey>().Queryable.Where(a => a.IsActive);

            return viewModel;
        }
    }

    public class SingleAnswerSurveyResponseViewModel
    {
        public Survey Survey { get; set; }
        public SurveyResponse PendingSurveyResponse { get; set; }
        [DisplayName("Total Questions")]
        public int TotalActiveQuestions { get; set; }
        [DisplayName("Answered")]
        public int AnsweredQuestions { get; set; }
        public IList<Question> Questions { get; set; }
        public Question CurrentQuestion { get; set; }
        public bool CannotContinue { get; set; }
        public bool PendingSurveyResponseExists { get; set; }
        public SurveyResponse SurveyResponse { get; set; }
        public QuestionAnswerParameter SurveyAnswer { get; set; }
        public bool FromAdmin { get; set; }

        public static SingleAnswerSurveyResponseViewModel Create(IRepository repository, Survey survey, SurveyResponse pendingSurveyResponse)
        {
            Check.Require(repository != null, "Repository must be supplied");
            Check.Require(survey != null);

            var viewModel = new SingleAnswerSurveyResponseViewModel{Survey = survey, PendingSurveyResponse = pendingSurveyResponse, SurveyResponse = new SurveyResponse(survey)};
            viewModel.Questions = viewModel.Survey.Questions
                .Where(a => a.IsActive && a.Category != null && a.Category.IsActive && a.Category.IsCurrentVersion)
                .OrderBy(a => a.Order).ToList();
            viewModel.TotalActiveQuestions = viewModel.Questions.Count;
            if (viewModel.PendingSurveyResponse != null)
            {
                viewModel.PendingSurveyResponseExists = true;
                viewModel.AnsweredQuestions = viewModel.PendingSurveyResponse.Answers.Count;
                var answeredQuestionIds = viewModel.PendingSurveyResponse.Answers.Select(a => a.Question.Id).ToList();
                viewModel.CurrentQuestion = viewModel.Questions
                    .Where(a => !answeredQuestionIds.Contains(a.Id))
                    .OrderBy(a => a.Order)
                    .FirstOrDefault();
            }
            else
            {
                viewModel.PendingSurveyResponseExists = false;
                viewModel.AnsweredQuestions = 0;
                viewModel.CurrentQuestion = viewModel.Questions.FirstOrDefault();
            }

            return viewModel;
        }
    }

	/// <summary>
    /// ViewModel for the SurveyResponse class
    /// </summary>
    public class SurveyResponseViewModel
	{
		public SurveyResponse SurveyResponse { get; set; }
        public IList<Question> Questions { get; set; }
        public Survey Survey { get; set; }
	    public QuestionAnswerParameter[] SurveyAnswers;
 
		public static SurveyResponseViewModel Create(IRepository repository, Survey survey)
		{
			Check.Require(repository != null, "Repository must be supplied");
            Check.Require(survey != null);
			
			var viewModel = new SurveyResponseViewModel {SurveyResponse = new SurveyResponse(survey), Survey = survey};
		    //viewModel.SurveyResponse.Survey = survey;
		    viewModel.Questions = viewModel.Survey.Questions
                .Where(a => a.IsActive && a.Category != null && a.Category.IsActive && a.Category.IsCurrentVersion)
                .OrderBy(a => a.Order).ToList();            
			return viewModel;
		}
	}

    public class SurveyReponseDetailViewModel
    {
        public SurveyResponse SurveyResponse { get; set; }
        public IList<Scores> Scores { get; set; }

        public static SurveyReponseDetailViewModel Create(IRepository repository, SurveyResponse surveyResponse)
        {
            Check.Require(repository != null, "Repository must be supplied");
            Check.Require(surveyResponse != null);

            var viewModel = new SurveyReponseDetailViewModel {SurveyResponse = surveyResponse};

            //Get all the related categories that had answers.
            var relatedCategoryIds = viewModel.SurveyResponse.Answers.Select(x => x.Category.Id).Distinct().ToList();
            viewModel.Scores = new List<Scores>();
            foreach (var category in viewModel.SurveyResponse.Survey.Categories.Where(a => !a.DoNotUseForCalculations && relatedCategoryIds.Contains(a.Id)))
            {
                var score = new Scores();
                score.Category = category;
                var totalMax = repository.OfType<CategoryTotalMaxScore>().GetNullableById(category.Id);
                if (totalMax == null) //No Questions most likely
                {
                    continue;
                }
                score.MaxScore = totalMax.TotalMaxScore; 

                //score.MaxScore = repository.OfType<CategoryTotalMaxScore>().GetNullableById(category.Id).TotalMaxScore;
                score.TotalScore = viewModel.SurveyResponse.Answers.Where(a => a.Category == category).Sum(b => b.Score);
                score.Percent = (score.TotalScore / score.MaxScore) * 100m;
                score.Rank = category.Rank;
                viewModel.Scores.Add(score);
            }


            return viewModel;
        }
    }


    public class QuestionAnswerParameter
    {
        public int QuestionId { get; set; }
        public string Answer { get; set; }
        public int ResponseId { get; set; }
        public bool Invalid { get; set; } 
        public string Message { get; set; } //Error message when invalid
        public int Score { get; set; } //Score when Valid
        public int? OpenEndedNumericAnswer { get; set; } //When Open ended and could parse int (may need to change for time, or other types currently not supported)
    }

    public class Scores
    {
        public Category Category { get; set; }
        public decimal MaxScore { get; set; }
        public decimal TotalScore { get; set; }
        public decimal Percent { get; set; }
        public int Rank { get; set; }
    }
}

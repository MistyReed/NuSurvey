﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuSurvey.Core.Domain;
using UCDArch.Testing.Fakes;

namespace NuSurvey.Tests.Core.Helpers
{
    public class FakeAnswers : ControllerRecordFakes<Answer>
    {
        protected override Answer CreateValid(int i)
        {
            return CreateValidEntities.Answer(i);
        }
    }

    public class FakeSurveys : ControllerRecordFakes<Survey>
    {
        protected override Survey CreateValid(int i)
        {
            return CreateValidEntities.Survey(i);
        }
    }

    public class FakeCategories : ControllerRecordFakes<Category>
    {
        protected override Category CreateValid(int i)
        {
            return CreateValidEntities.Category(i);
        }
    }

    public class FakeCategoryGoals : ControllerRecordFakes<CategoryGoal>
    {
        protected override CategoryGoal CreateValid(int i)
        {
            return CreateValidEntities.CategoryGoal(i);
        }
    }
}

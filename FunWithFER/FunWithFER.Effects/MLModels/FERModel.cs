﻿using System;
using System.Threading.Tasks;
using Windows.AI.MachineLearning.Preview;
using Windows.Foundation;
using Windows.Storage;

// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
namespace FunWithFER.Effects.MLModels
{
    public sealed class FERModel
    {
        private LearningModelPreview learningModel;

        internal static async Task<FERModel> CreateFERModel(StorageFile file)
        {
            var learningModel = await LearningModelPreview.LoadModelFromStorageFileAsync(file);

            return new FERModel {learningModel = learningModel};
        }

        internal async Task<FERModelOutput> EvaluateAsync(FERModelInput input)
        {
            var output = new FERModelOutput();

            var binding = new LearningModelBindingPreview(learningModel);
            binding.Bind("Input2505", input.Input2505);
            binding.Bind("Softmax2997_Output_0", output.Softmax2997_Output_0);

            var evalResult = await learningModel.EvaluateAsync(binding, string.Empty);

            return output;
        }
    }
}

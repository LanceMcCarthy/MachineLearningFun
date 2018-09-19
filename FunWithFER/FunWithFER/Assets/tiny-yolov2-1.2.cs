using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Storage;
using Windows.AI.MachineLearning.Preview;

// 15fb3d9ea39f4068ad44a49de20faedc

namespace FunWithFER
{
    public sealed class 15fb3d9ea39f4068ad44a49de20faedcModelInput
    {
        public VideoFrame image { get; set; }
    }

    public sealed class 15fb3d9ea39f4068ad44a49de20faedcModelOutput
    {
        public IList<float> grid { get; set; }
        public 15fb3d9ea39f4068ad44a49de20faedcModelOutput()
        {
            this.grid = new List<float>();
        }
    }

    public sealed class 15fb3d9ea39f4068ad44a49de20faedcModel
    {
        private LearningModelPreview learningModel;
        public static async Task<15fb3d9ea39f4068ad44a49de20faedcModel> Create15fb3d9ea39f4068ad44a49de20faedcModel(StorageFile file)
        {
            LearningModelPreview learningModel = await LearningModelPreview.LoadModelFromStorageFileAsync(file);
            15fb3d9ea39f4068ad44a49de20faedcModel model = new 15fb3d9ea39f4068ad44a49de20faedcModel();
            model.learningModel = learningModel;
            return model;
        }
        public async Task<15fb3d9ea39f4068ad44a49de20faedcModelOutput> EvaluateAsync(15fb3d9ea39f4068ad44a49de20faedcModelInput input) {
            15fb3d9ea39f4068ad44a49de20faedcModelOutput output = new 15fb3d9ea39f4068ad44a49de20faedcModelOutput();
            LearningModelBindingPreview binding = new LearningModelBindingPreview(learningModel);
            binding.Bind("image", input.image);
            binding.Bind("grid", output.grid);
            LearningModelEvaluationResultPreview evalResult = await learningModel.EvaluateAsync(binding, string.Empty);
            return output;
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using Forge.Forms.FormBuilding;
using Forge.Forms.Validation;

namespace Forge.Forms.DynamicExpressions
{
    public sealed class ConvertedDataBinding : IValueProvider
    {
        public ConvertedDataBinding(string propertyPath, BindingOptions bindingOptions,
            List<IValidatorProvider> validationRules, ReplacementPipe replacementPipe,
            Func<IResourceContext, IErrorStringProvider> conversionErrorStringProvider)
        {
            PropertyPath = propertyPath;
            BindingOptions = bindingOptions ?? throw new ArgumentNullException(nameof(bindingOptions));
            ReplacementPipe = replacementPipe ?? throw new ArgumentNullException(nameof(replacementPipe));
            ValidationRules = validationRules ?? new List<IValidatorProvider>();
            ConversionErrorStringProvider = conversionErrorStringProvider;
        }

        public string PropertyPath { get; }

        public BindingOptions BindingOptions { get; }

        public List<IValidatorProvider> ValidationRules { get; }

        public ReplacementPipe ReplacementPipe { get; }

        public Func<IResourceContext, IErrorStringProvider> ConversionErrorStringProvider { get; }

        public BindingBase ProvideBinding(IResourceContext context)
        {
            var binding = context.CreateModelBinding(PropertyPath);
            BindingOptions.Apply(binding);
            var deserializer = ReplacementPipe.CreateDeserializer(context);
            binding.Converter = new StringTypeConverter(deserializer);
            binding.ValidationRules.Add(new ConversionValidator(deserializer, ConversionErrorStringProvider(context),
                binding.ConverterCulture));
            var pipe = new ValidationPipe();
            foreach (var validatorProvider in ValidationRules)
            {
                binding.ValidationRules.Add(validatorProvider.GetValidator(context, pipe));
            }

            binding.ValidationRules.Add(pipe);
            return binding;
        }

        public object ProvideValue(IResourceContext context)
        {
            return ProvideBinding(context);
        }
    }

    public sealed class ConvertedNumericDataBinding : IValueProvider
    {
        public ConvertedNumericDataBinding(string propertyPath, BindingOptions bindingOptions,
            List<IValidatorProvider> validationRules, NumericReplacementPipe replacementPipe,
            Func<IResourceContext, IErrorStringProvider> conversionErrorStringProvider,
            IValueProvider numberStyles)
        {
            PropertyPath = propertyPath;
            BindingOptions = bindingOptions ?? throw new ArgumentNullException(nameof(bindingOptions));
            ReplacementPipe = replacementPipe ?? throw new ArgumentNullException(nameof(replacementPipe));
            ValidationRules = validationRules ?? new List<IValidatorProvider>();
            ConversionErrorStringProvider = conversionErrorStringProvider;
            NumberStyles = numberStyles;
        }

        public string PropertyPath { get; }

        public BindingOptions BindingOptions { get; }

        public List<IValidatorProvider> ValidationRules { get; }

        public NumericReplacementPipe ReplacementPipe { get; }

        public Func<IResourceContext, IErrorStringProvider> ConversionErrorStringProvider { get; }

        public IValueProvider NumberStyles { get; }

        public BindingBase ProvideBinding(IResourceContext context)
        {
            var binding = context.CreateModelBinding(PropertyPath);
            var numberStylesProxy = NumberStyles.GetBestMatchingProxy(context);
            BindingOptions.Apply(binding);
            var deserializer = ReplacementPipe.CreateDeserializer(context);
            object AppliedDeserializer(string val, CultureInfo cul) => deserializer(val, cul, numberStylesProxy.Value as NumberStyles?);
            binding.Converter = new StringTypeConverter(AppliedDeserializer);
            binding.ValidationRules.Add(new ConversionValidator(AppliedDeserializer, ConversionErrorStringProvider(context),
                binding.ConverterCulture));
            var pipe = new ValidationPipe();
            foreach (var validatorProvider in ValidationRules)
            {
                binding.ValidationRules.Add(validatorProvider.GetValidator(context, pipe));
            }

            binding.ValidationRules.Add(pipe);
            return binding;
        }

        public object ProvideValue(IResourceContext context)
        {
            return ProvideBinding(context);
        }
    }
}

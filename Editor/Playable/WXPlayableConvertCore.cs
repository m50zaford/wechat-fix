using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WeChatWASM
{
    public class WXPlayableConvertCore
    {
        static WXPlayableConvertCore() { }
        public static WXPlayableEditorScriptObject config => UnityUtil.GetPlayableEditorConf();

        public static WXConvertCore.WXExportError DoExport(bool buildWebGL = true)
        {
            WXConvertCore.isPlayableBuild = true;
            // var preCheckResult = WXConvertCore.PreCheck();
            // if (preCheckResult != WXConvertCore.WXExportError.SUCCEED)
            // {
            //   WXConvertCore.isPlayableBuild = false;
            //   return preCheckResult;
            // }
            // WXConvertCore.PreInit();
            var exportResult = WXConvertCore.DoExport();

            WXConvertCore.isPlayableBuild = false;
            return exportResult;
        }

        public static WXEditorScriptObject GetFakeScriptObject()
        {
            return SetDefaultProperties(ConvertPlayableConfigToCommon(config));
        }

        public static WXEditorScriptObject ConvertPlayableConfigToCommon(
            WXPlayableEditorScriptObject source,
            WXEditorScriptObject target = null)
        {
            // 대상 인스턴스를 생성하거나 기존 인스턴스를 사용합니다
            var newTarget = target ?? ScriptableObject.CreateInstance<WXEditorScriptObject>();

            // 직렬화 방식으로 공용 필드를 깊은 복사합니다
            var so = new SerializedObject(newTarget);

            // 소스 객체의 모든 필드를 순회합니다
            var sourceType = source.GetType();
            foreach (var sourceField in sourceType.GetFields(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic))
            {
                // readonly 필드는 건너뜁니다
                if (sourceField.IsInitOnly) continue;

                // 대상 객체에서 해당 필드를 찾습니다
                var targetField = typeof(WXEditorScriptObject).GetField(
                    sourceField.Name,
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);

                // if (targetField != null && !targetField.FieldType.IsValueType && !targetField.FieldType.IsEnum)
                // {
                //   // 필드 값을 복사합니다
                //   // var value = sourceField.GetValue(source);
                //   // targetField.SetValue(newTarget, value);
                //   // 하위 객체 속성을 재귀적으로 복사합니다
                //   var subObj = targetField.GetValue(newTarget) ?? Activator.CreateInstance(targetField.FieldType);
                //   CopySubObjectProperties(value, subObj);
                //   targetField.SetValue(newTarget, subObj);
                // }

                // if (targetField != null &&
                //     (targetField.FieldType.IsAssignableFrom(sourceField.FieldType) ||
                //     (targetField.FieldType.IsValueType && sourceField.FieldType.IsValueType &&
                //      targetField.FieldType == sourceField.FieldType)))
                // {
                // 필드 값을 복사합니다
                var value = sourceField.GetValue(source);
                // 중첩 객체 유형의 필드를 특수 처리합니다
                if (value != null && !targetField.FieldType.IsValueType && !targetField.FieldType.IsEnum)
                {
                    // 하위 객체 속성을 재귀적으로 복사합니다
                    var subObj = targetField.GetValue(newTarget) ?? Activator.CreateInstance(targetField.FieldType);
                    CopySubObjectProperties(value, subObj);
                    targetField.SetValue(newTarget, subObj);
                }
                else
                {
                    targetField.SetValue(newTarget, value);
                }
                // }
            }

            // 수정 사항을 직렬화 객체에 적용합니다
            so.ApplyModifiedProperties();
            return newTarget;
        }

        private static void CopySubObjectProperties(object source, object target)
        {
            var sourceType = source.GetType();
            var targetType = target.GetType();

            foreach (var sourceField in sourceType.GetFields(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic))
            {
                if (sourceField.IsInitOnly) continue;

                var targetField = targetType.GetField(
                    sourceField.Name,
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);

                if (targetField != null &&
                    (targetField.FieldType.IsAssignableFrom(sourceField.FieldType) ||
                    (targetField.FieldType.IsValueType && sourceField.FieldType.IsValueType &&
                     targetField.FieldType == sourceField.FieldType)))
                {
                    var value = sourceField.GetValue(source);
                    targetField.SetValue(target, value);
                }
            }
        }

        public static WXEditorScriptObject SetDefaultProperties(WXEditorScriptObject target)
        {
            target.ProjectConf.CDN = "";
            target.ProjectConf.assetLoadType = 1;
            target.ProjectConf.compressDataPackage = true;

            target.CompileOptions.showMonitorSuggestModal = false;
            return target;
        }
    }
}

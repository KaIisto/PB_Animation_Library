using System;
using System.Globalization;
using System.Text;
using PB_AnimationLibrary.Exchange;

namespace PB_AnimationLibrary
{
    internal static class PoseSequenceBakeJsonWriter
    {
        internal static bool TrySerialize(
            PoseSequenceBakeExchangeFile exchange,
            out string json,
            out string error)
        {
            json = string.Empty;
            error = string.Empty;

            if (exchange == null)
            {
                error = "exchange is null";
                return false;
            }

            if (exchange.tracks == null ||
                exchange.tracks.Length == 0)
            {
                error = "no animated tracks";
                return false;
            }

            try
            {
                StringBuilder builder =
                    new StringBuilder(4096);

                builder.AppendLine("{");
                AppendStringField(
                    builder,
                    1,
                    "schema",
                    exchange.schema,
                    true);

                AppendIntField(
                    builder,
                    1,
                    "schemaVersion",
                    exchange.schemaVersion,
                    true);

                AppendStringField(
                    builder,
                    1,
                    "clipName",
                    exchange.clipName,
                    true);

                AppendStringField(
                    builder,
                    1,
                    "sourcePoseName",
                    exchange.sourcePoseName,
                    true);

                AppendStringField(
                    builder,
                    1,
                    "samplingRoot",
                    exchange.samplingRoot,
                    true);

                AppendStringField(
                    builder,
                    1,
                    "pathRoot",
                    exchange.pathRoot,
                    true);

                AppendFloatField(
                    builder,
                    1,
                    "frameRate",
                    exchange.frameRate,
                    true);

                AppendFloatField(
                    builder,
                    1,
                    "duration",
                    exchange.duration,
                    true);

                AppendIntField(
                    builder,
                    1,
                    "sourceKeyframeCount",
                    exchange.sourceKeyframeCount,
                    true);

                AppendIndent(
                    builder,
                    1);

                AppendJsonString(
                    builder,
                    "tracks");

                builder.AppendLine(": [");

                for (int trackIndex = 0;
                     trackIndex < exchange.tracks.Length;
                     ++trackIndex)
                {
                    PoseSequenceBakeTrack track =
                        exchange.tracks[trackIndex];

                    if (track == null)
                    {
                        error =
                            "track "
                            + trackIndex
                            + " is null";

                        return false;
                    }

                    AppendTrack(
                        builder,
                        track,
                        2,
                        trackIndex + 1 <
                        exchange.tracks.Length);
                }

                AppendIndent(
                    builder,
                    1);

                builder.AppendLine("]");
                builder.Append('}');

                json =
                    builder.ToString();

                return true;
            }
            catch (Exception exception)
            {
                error =
                    exception.GetType().Name
                    + ": "
                    + exception.Message;

                return false;
            }
        }

        private static void AppendTrack(
            StringBuilder builder,
            PoseSequenceBakeTrack track,
            int indent,
            bool trailingComma)
        {
            AppendIndent(
                builder,
                indent);

            builder.AppendLine("{");

            AppendStringField(
                builder,
                indent + 1,
                "path",
                track.path,
                true);

            AppendStringField(
                builder,
                indent + 1,
                "pathHash",
                track.pathHash,
                true);

            AppendBoolField(
                builder,
                indent + 1,
                "hasPosition",
                track.hasPosition,
                true);

            AppendBoolField(
                builder,
                indent + 1,
                "hasRotation",
                track.hasRotation,
                true);

            AppendPositionKeys(
                builder,
                track.positionKeys,
                indent + 1,
                true);

            AppendRotationKeys(
                builder,
                track.rotationKeys,
                indent + 1,
                false);

            AppendIndent(
                builder,
                indent);

            builder.Append('}');

            if (trailingComma)
                builder.Append(',');

            builder.AppendLine();
        }

        private static void AppendPositionKeys(
            StringBuilder builder,
            PoseSequenceBakePositionKey[] keys,
            int indent,
            bool trailingComma)
        {
            AppendIndent(
                builder,
                indent);

            AppendJsonString(
                builder,
                "positionKeys");

            builder.AppendLine(": [");

            PoseSequenceBakePositionKey[] safeKeys =
                keys ??
                new PoseSequenceBakePositionKey[0];

            for (int i = 0;
                 i < safeKeys.Length;
                 ++i)
            {
                PoseSequenceBakePositionKey key =
                    safeKeys[i];

                if (key == null)
                    continue;

                AppendIndent(
                    builder,
                    indent + 1);

                builder.Append("{ ");

                AppendInlineFloatField(
                    builder,
                    "time",
                    key.time,
                    true);

                AppendInlineFloatField(
                    builder,
                    "x",
                    key.x,
                    true);

                AppendInlineFloatField(
                    builder,
                    "y",
                    key.y,
                    true);

                AppendInlineFloatField(
                    builder,
                    "z",
                    key.z,
                    false);

                builder.Append(" }");

                if (i + 1 <
                    safeKeys.Length)
                {
                    builder.Append(',');
                }

                builder.AppendLine();
            }

            AppendIndent(
                builder,
                indent);

            builder.Append(']');

            if (trailingComma)
                builder.Append(',');

            builder.AppendLine();
        }

        private static void AppendRotationKeys(
            StringBuilder builder,
            PoseSequenceBakeRotationKey[] keys,
            int indent,
            bool trailingComma)
        {
            AppendIndent(
                builder,
                indent);

            AppendJsonString(
                builder,
                "rotationKeys");

            builder.AppendLine(": [");

            PoseSequenceBakeRotationKey[] safeKeys =
                keys ??
                new PoseSequenceBakeRotationKey[0];

            for (int i = 0;
                 i < safeKeys.Length;
                 ++i)
            {
                PoseSequenceBakeRotationKey key =
                    safeKeys[i];

                if (key == null)
                    continue;

                AppendIndent(
                    builder,
                    indent + 1);

                builder.Append("{ ");

                AppendInlineFloatField(
                    builder,
                    "time",
                    key.time,
                    true);

                AppendInlineFloatField(
                    builder,
                    "x",
                    key.x,
                    true);

                AppendInlineFloatField(
                    builder,
                    "y",
                    key.y,
                    true);

                AppendInlineFloatField(
                    builder,
                    "z",
                    key.z,
                    true);

                AppendInlineFloatField(
                    builder,
                    "w",
                    key.w,
                    false);

                builder.Append(" }");

                if (i + 1 <
                    safeKeys.Length)
                {
                    builder.Append(',');
                }

                builder.AppendLine();
            }

            AppendIndent(
                builder,
                indent);

            builder.Append(']');

            if (trailingComma)
                builder.Append(',');

            builder.AppendLine();
        }

        private static void AppendStringField(
            StringBuilder builder,
            int indent,
            string name,
            string value,
            bool trailingComma)
        {
            AppendIndent(
                builder,
                indent);

            AppendJsonString(
                builder,
                name);

            builder.Append(": ");

            AppendJsonString(
                builder,
                value ?? string.Empty);

            if (trailingComma)
                builder.Append(',');

            builder.AppendLine();
        }

        private static void AppendIntField(
            StringBuilder builder,
            int indent,
            string name,
            int value,
            bool trailingComma)
        {
            AppendIndent(
                builder,
                indent);

            AppendJsonString(
                builder,
                name);

            builder.Append(": ");
            builder.Append(
                value.ToString(
                    CultureInfo.InvariantCulture));

            if (trailingComma)
                builder.Append(',');

            builder.AppendLine();
        }

        private static void AppendFloatField(
            StringBuilder builder,
            int indent,
            string name,
            float value,
            bool trailingComma)
        {
            AppendIndent(
                builder,
                indent);

            AppendJsonString(
                builder,
                name);

            builder.Append(": ");
            AppendJsonFloat(
                builder,
                value);

            if (trailingComma)
                builder.Append(',');

            builder.AppendLine();
        }

        private static void AppendBoolField(
            StringBuilder builder,
            int indent,
            string name,
            bool value,
            bool trailingComma)
        {
            AppendIndent(
                builder,
                indent);

            AppendJsonString(
                builder,
                name);

            builder.Append(": ");
            builder.Append(
                value
                    ? "true"
                    : "false");

            if (trailingComma)
                builder.Append(',');

            builder.AppendLine();
        }

        private static void AppendInlineFloatField(
            StringBuilder builder,
            string name,
            float value,
            bool trailingComma)
        {
            AppendJsonString(
                builder,
                name);

            builder.Append(": ");
            AppendJsonFloat(
                builder,
                value);

            if (trailingComma)
                builder.Append(", ");
        }

        private static void AppendJsonFloat(
            StringBuilder builder,
            float value)
        {
            if (float.IsNaN(value) ||
                float.IsInfinity(value))
            {
                builder.Append('0');
                return;
            }

            builder.Append(
                value.ToString(
                    "R",
                    CultureInfo.InvariantCulture));
        }

        private static void AppendJsonString(
            StringBuilder builder,
            string value)
        {
            builder.Append('"');

            string source =
                value ?? string.Empty;

            for (int i = 0;
                 i < source.Length;
                 ++i)
            {
                char character =
                    source[i];

                switch (character)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;

                    case '\\':
                        builder.Append("\\\\");
                        break;

                    case '\b':
                        builder.Append("\\b");
                        break;

                    case '\f':
                        builder.Append("\\f");
                        break;

                    case '\n':
                        builder.Append("\\n");
                        break;

                    case '\r':
                        builder.Append("\\r");
                        break;

                    case '\t':
                        builder.Append("\\t");
                        break;

                    default:
                        if (character < 32)
                        {
                            builder.Append("\\u");
                            builder.Append(
                                ((int)character)
                                .ToString("X4"));
                        }
                        else
                        {
                            builder.Append(
                                character);
                        }

                        break;
                }
            }

            builder.Append('"');
        }

        private static void AppendIndent(
            StringBuilder builder,
            int indent)
        {
            for (int i = 0;
                 i < indent;
                 ++i)
            {
                builder.Append("  ");
            }
        }
    }
}

using Aardvark.Base;
using System.Linq;

namespace Aardvark.Data.Photometry
{
    /// <summary>
    /// Sampler for LightMeasurementData
    /// The data is transformed to equidistant measurements so it can be addressed faster.
    /// It holds a Matrix (with border) for CPU sampling and a PixImage for the GPU.
    /// </summary>
    public class IntensityProfileSampler
    {
        LightMeasurementData m_data;
        
        PixImage<float> m_image; // texture for GPU sampling
        Matrix<float> m_matrixWithBorder; // matrix for CPU sampling (has two rows more to simplify filtering)

        V4f m_addressingParams;
        V4f m_imageOffsetScale;
        V2d m_scale;

        /// <summary>
        /// Image for GPU sampling
        /// </summary>
        public PixImage<float> Image { get { return m_image; } }

        /// <summary>
        /// Addressing parameters for GPU sampling
        /// </summary>
        public V4f AddressingParameters { get { return m_addressingParams; } }

        /// <summary>
        /// Image offset and scale for GPU sampling
        /// </summary>
        public V4f ImageOffsetScale { get { return m_imageOffsetScale; } }

        /// <summary>
        /// Creates an IntensityProfileSampler for a LightMeasurementData
        /// </summary>
        public IntensityProfileSampler(LightMeasurementData data)
        {
            m_data = data;

            var equidistant = data.BuildEquidistantMatrix();

            m_image = new PixImage<float>(equidistant.Map(t => (float)t));

            var originalMatrix = m_image.Matrix;
            var matrixWithBorder = new Matrix<float>(originalMatrix.SX + 2, originalMatrix.SY + 2) { FX = -1, FY = -1 };
            matrixWithBorder.Set(0.0f);
            matrixWithBorder.SubCenter(new Border2l(1)).Set(originalMatrix);

            if (data.HorizontalSymmetry == HorizontalSymmetryMode.None)
            {
                var sx = originalMatrix.SX;
                var sy = originalMatrix.SY;

                // Copy Last-row to Top-border
                Matrix<float> bottomRow = matrixWithBorder.SubMatrix(-1L, sy - 1, sx + 2, 1L);
                matrixWithBorder.SubMatrix(-1L, -1L, sx + 2, 1L).Set(bottomRow);

                // Copy First-row to Bottom-border
                Matrix<float> topRow = matrixWithBorder.SubMatrix(-1L, 0L, sx + 2, 1L);
                matrixWithBorder.SubMatrix(-1L, sy, sx + 2, 1L).Set(topRow);
            }

            m_matrixWithBorder = matrixWithBorder;

            var scale = (V2d)m_matrixWithBorder.Size - 3; // 2 pixel larger size (border) and 1 for correct scale => -3 of matrixWithBorder / -1 of originalMatrix

            if (data.HorizontalSymmetry == HorizontalSymmetryMode.None) scale.Y++; // special case, where horizontal-symmetry needs wrap!
            if (data.HorizontalSymmetry == HorizontalSymmetryMode.Full) scale.Y = 0;

            m_scale = scale;

            m_addressingParams = BuildAddressingParameters();

            m_imageOffsetScale = BuildImageOffsetScale();

            //m_texture = new PixTexture2d(new PixImageMipMap(m_image), false);
        }

        private V4f BuildImageOffsetScale()
        {
            var size = m_image.Matrix.Size;

            float offsetVertical = 0.5f / size.X; // address to first texel center
            float scaleVertical = (size.X - 1.0f) / size.X; // address to last texel center

            float offsetHorizontal = 0.5f / size.Y; // address to first texel center
            float scaleHorizontal = (size.Y - 1.0f) / size.Y; // address to last texel center

            if (m_data.HorizontalSymmetry == HorizontalSymmetryMode.None)
            {
                // wrap first data row to 360° in case of measurement from 0 to e.g. 345 (LDT)
                if (m_data.HorizontalAngles.Last() - m_data.HorizontalAngles.First() != 360)
                    scaleHorizontal = 1.0f; // wrap address to first texel center
            }

            return new V4f(offsetVertical, scaleVertical, offsetHorizontal, scaleHorizontal);
        }

        private V4f BuildAddressingParameters()
        {
            // in case of full symmetry its either possible to have 0 to (360-measureOffset) or 0 to 360
            // horizontal scale only relevant in half or quarter symmetry cases
            var scaleHorizontal = (m_data.HorizontalSymmetry != HorizontalSymmetryMode.Full && m_data.HorizontalSymmetry != HorizontalSymmetryMode.None) ? 360.0f / Fun.Max(1, (m_data.HorizontalAngles.Last() - m_data.HorizontalAngles.First()).Abs()) : 1.0;
            var scaleVertical = 180.0f / Fun.Max(1, (m_data.VerticalAngles.Last() - m_data.VerticalAngles.First()).Abs());
            var offsetHorizontal = -m_data.HorizontalAngles.First() / 360.0f;
            var offsetVertical = -m_data.VerticalAngles.First() / 180.0f;
                        
            return new V4f(offsetVertical, scaleVertical, offsetHorizontal, scaleHorizontal);
        }

        /// <summary>
        /// Returns the intensity for the given direction vector using linear interpolation in [cd].
        /// The direction vector is expected to be normalized.
        /// </summary>
        public double GetIntensity(V3d dir)
        {
            // Vertical Texture coords
            var phi = (1 - Fun.Acos(Fun.Clamp(dir.Z, -1, 1)) * Constant.PiInv); // map to 0..1

            var u = Fun.Clamp((phi + m_addressingParams.X) * m_addressingParams.Y, 0, 1);

            // Horizontal Texture coords
            // C0:   atan2( 0  1)  =   0
            // C90:  atan2( 1  0)  =  90
            // C180: atan2( 0 -1)  = 180/-180
            // C270: atan2(-1  0)  = -90
            // normalize [-pi..pi] to [0..1] -> invert vector and add 180°/0.5
            var theta = Fun.Atan2(-dir.Y, -dir.X) * Constant.PiInv * 0.5 + 0.5;

            var v = (1.0 - Fun.Abs(1.0f - Fun.Abs(((theta + m_addressingParams.Z) * m_addressingParams.W) % 2.0)));

            var uv = (new V2d(u, v) * m_scale); // the +1 offset in Y because m_matrixWithBorder contains border is handled by the matrix FirstIndex (FX, FY)
            
            return m_matrixWithBorder.Sample4Clamped(uv, Fun.Lerp, Fun.Lerp);
        }
    }
}

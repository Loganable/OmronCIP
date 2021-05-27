namespace OmronCIP
{
    /// <summary>
    /// 结果类：用于保存结果状态，信息和内容
    /// </summary>
    /// /// <remarks>
    /// <see cref="IsSuccess"/> : 结果状态
    /// <see cref="Message"/>   : 结果信息
    /// <see cref="Content"/>   : 结果内容
    /// </remarks>
    public class CIPResult
    {
        #region Properties
        /// <summary>
        /// 结果状态
        /// </summary>
        public bool IsSuccess { get; set; } = true;

        /// <summary>
        /// 结果信息，失败则为错误信息
        /// </summary>
        public string Message { get; set; } = "成功";

        /// <summary>
        /// 结果内容
        /// </summary>
        public object Content { get; set; } = null;
        #endregion

        #region Constructor
        public CIPResult(){}
        #endregion
    }
}

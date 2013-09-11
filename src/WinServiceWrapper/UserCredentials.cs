namespace WinServiceWrapper
{
	public class UserCredentials
	{
		public bool IsValid()
		{
			return (!string.IsNullOrWhiteSpace(Domain))
				&& (!string.IsNullOrWhiteSpace(UserName))
				&& (!string.IsNullOrWhiteSpace(Password));
		}

		public string Domain { get; set; }
		public string UserName { get; set; }
		public string Password { get; set; }
	}
}
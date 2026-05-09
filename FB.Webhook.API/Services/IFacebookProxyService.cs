using System.Threading.Tasks;

namespace FB.Webhook.API.Services;

public interface IFacebookProxyService
{
    Task<string> GetPostsAsync();
    Task<string> CreatePostAsync(string message);
    Task<string> GetCommentsAsync(string postId);
}

using Microsoft.AspNetCore.Http.HttpResults;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var  commentsList = new List<Comment>();

app.MapGet("/", () => "HelloW!");

app.MapPost("/api/comment/new", (Comment comment) => {
    commentsList.Add(comment);
    return TypedResults.Created("/api/comment/list/{id}", comment);
});

app.MapGet("/api/comment/list/{id}", Results<Ok<Comment>, NotFound> (int id) => {
    var commentFound = commentsList.FirstOrDefault(m => m.content_id == id);
    return  commentFound is null ? TypedResults.NotFound() : TypedResults.Ok(commentFound);
});

app.Run();
public record Comment(int content_id, string comment, string email){}

using FlashRevise.Models;

namespace FlashRevise.Services;

/// <summary>Suivi des livres et manuels associés aux objectifs d'étude.</summary>
public interface IBookService
{
    Task<List<Book>> GetAllAsync();
    Task<int> SaveAsync(Book book);
    Task DeleteAsync(int id);

    /// <summary>Applique une progression (0 à 1) et renvoie la valeur effectivement enregistrée.</summary>
    Task<double> UpdateProgressAsync(int id, double progress);
}

/// <inheritdoc cref="IBookService"/>
public sealed class BookService(Data.Repositories.IBookRepository books) : IBookService
{
    public Task<List<Book>> GetAllAsync() => books.GetAllAsync();

    public Task<int> SaveAsync(Book book)
    {
        book.Progress = Math.Clamp(book.Progress, 0, 1);
        return books.SaveAsync(book);
    }

    public Task DeleteAsync(int id) => books.DeleteAsync(id);

    public async Task<double> UpdateProgressAsync(int id, double progress)
    {
        var all = await books.GetAllAsync().ConfigureAwait(false);
        var book = all.FirstOrDefault(b => b.Id == id);
        if (book is null)
        {
            return 0;
        }

        book.Progress = Math.Clamp(progress, 0, 1);
        await books.SaveAsync(book).ConfigureAwait(false);
        return book.Progress;
    }
}
